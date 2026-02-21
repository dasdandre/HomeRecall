using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

using Makaretu.Dns;

namespace HomeRecall.Services.Strategies;

public class ShellyStrategy : IMqttDeviceStrategy, IMdnsDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Shelly;

    private readonly ILogger<ShellyStrategy> _logger;

    public ShellyStrategy(ILogger<ShellyStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            // Gen1 uses /settings for Name, but it might be protected.
            // /shelly is public.

            // Try /settings first (Auth might fail)
            try

            {
                var settings = await httpClient.GetFromJsonAsync<ShellySettings>($"http://{ip}/settings");
                if (settings?.Type != null)
                {
                    string name = !string.IsNullOrWhiteSpace(settings.Name) ? settings.Name :

                                  !string.IsNullOrWhiteSpace(settings.Device?.Hostname) ? settings.Device.Hostname :

                                  $"Shelly-{ip.Split('.').Last()}";


                    return new DiscoveredDevice
                    {

                        Type = DeviceType.Shelly,

                        Name = name,

                        FirmwareVersion = "Gen1",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Hostname = settings.Device?.Hostname, MacAddress = settings.Device?.Mac, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Auth required. Fallback to /shelly (public)
                var info = await httpClient.GetFromJsonAsync<ShellyInfo>($"http://{ip}/shelly");
                if (info?.Type != null)
                {
                    return new DiscoveredDevice
                    {

                        Type = DeviceType.Shelly,

                        Name = $"Shelly-Locked-{ip.Split('.').Last()}",

                        FirmwareVersion = "Gen1",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, MacAddress = info.Mac, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
        }
        catch { }
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        if (device.Interfaces == null || device.Interfaces.Count == 0)
        {
            _logger.LogWarning($"No interfaces found for {device.Name} during backup.");
            return new DeviceBackupResult(new List<BackupFile>(), string.Empty);
        }

        var interfacesToTry = device.Interfaces
            .OrderByDescending(i => i.Type == NetworkInterfaceType.Ethernet)
            .ToList();

        _logger.LogDebug($"Attempting backup for {device.Name} across {interfacesToTry.Count} interfaces. Preference: Ethernet first.");

        foreach (var netInterface in interfacesToTry)
        {
            var ip = netInterface.IpAddress;
            if (string.IsNullOrEmpty(ip)) continue;

            _logger.LogTrace($"Trying to backup {device.Name} using interface IP {ip} ({netInterface.Type})...");

            try
            {
                var data = await httpClient.GetByteArrayAsync($"http://{ip}/settings");
                var files = new List<BackupFile> { new("settings.json", data) };
                _logger.LogTrace($"Successfully downloaded settings.json from {ip} for {device.Name}.");

                string version = string.Empty;
                try
                {
                    var info = await httpClient.GetFromJsonAsync<ShellyInfo>($"http://{ip}/shelly");
                    if (info?.Fw != null)
                    {
                        version = info.Fw;
                        _logger.LogTrace($"Retrieved firmware version {version} from {ip}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Could not retrieve firmware version from {ip} for {device.Name}.");
                }

                _logger.LogDebug($"Backup for {device.Name} succeeded using interface IP {ip} ({netInterface.Type}).");
                return new DeviceBackupResult(files, version);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Backup failed for {device.Name} on interface IP {ip} ({netInterface.Type}). Falling back to next interface if available...");
                continue;
            }
        }

        _logger.LogWarning($"Backup failed for all interfaces of {device.Name}.");
        return new DeviceBackupResult(new List<BackupFile>(), string.Empty);
    }

    public DiscoveredDevice? DiscoverFromMqtt(string topic, string payload)
    {
        // Shelly Gen 1 announces on shelly/+/announce
        if (topic.StartsWith("shelly/") && topic.EndsWith("/announce"))
        {
            try
            {
                var announce = JsonSerializer.Deserialize<ShellyAnnounce>(payload);
                if (announce != null && !string.IsNullOrEmpty(announce.Ip))
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.Shelly,
                        Name = $"Shelly-{announce.Ip.Split('.').Last()}",
                        FirmwareVersion = "Gen1",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = announce.Ip, MacAddress = announce.Mac, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch { }
        }
        return null;
    }

    public IEnumerable<string> MqttDiscoveryTopics => new[] { "shelly/+/announce" };

    public MqttDiscoveryMessage? DiscoveryMessage => null;

    public IEnumerable<string> MdnsServiceTypes => new[] { "_http._tcp.local" };

    public DiscoveredDevice? DiscoverFromMdns(MessageEventArgs eventArgs)
    {
        var message = eventArgs.Message;

        // Verify PTR is _http._tcp.local
        var ptrRecords = message.Answers.OfType<PTRRecord>().Concat(message.AdditionalRecords.OfType<PTRRecord>());
        bool isHttp = ptrRecords.Any(ptr => ptr.DomainName.ToString().Contains("_http._tcp.local", StringComparison.OrdinalIgnoreCase));

        if (!isHttp) return null;

        var aRecord = message.Answers.OfType<ARecord>().Concat(message.AdditionalRecords.OfType<ARecord>()).FirstOrDefault();
        if (aRecord == null) return null;

        var ip = aRecord.Address.ToString();
        var hostname = aRecord.Name.ToString().Replace(".local", "");

        // Gen1 Shelly hostnames look like "shelly1-AABBCC" or "shellyix3-..."

        if (!hostname.StartsWith("shelly", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? mac = null;

        // TXT records on Gen1 rarely have MAC, but we check anyway. Gen1 usually has no 'gen2' indicator.
        var txtRecords = message.Answers.OfType<TXTRecord>().Concat(message.AdditionalRecords.OfType<TXTRecord>());
        foreach (var txt in txtRecords)
        {
            foreach (var s in txt.Strings)
            {
                if (s.StartsWith("mac=", StringComparison.OrdinalIgnoreCase))
                {
                    mac = s.Substring(4).Replace(":", "");
                }
                if (s.Contains("gen2", StringComparison.OrdinalIgnoreCase))
                {
                    // This strategy is strictly for Gen1. Gen2+ uses _shelly._tcp.local now,
                    // but if it ever broadcasts _http with gen2, ignore it here.
                    return null;
                }
            }
        }

        if (string.IsNullOrEmpty(mac))
        {
            // Extract from hostname e.g. shelly1-AABBCC
            var parts = hostname.Split('-');
            if (parts.Length > 1)
            {
                mac = parts.Last();
            }
        }

        var discoveredDevice = new DiscoveredDevice
        {
            Type = DeviceType.Shelly,
            Name = hostname,
            FirmwareVersion = "Gen1"
        };


        discoveredDevice.Interfaces.Add(new NetworkInterface
        {
            IpAddress = ip,
            Hostname = hostname,
            MacAddress = mac ?? "",
            Type = NetworkInterfaceType.Wifi
        });

        return discoveredDevice;
    }

    private class ShellyAnnounce
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("mac")] public string? Mac { get; set; }
        [JsonPropertyName("ip")] public string? Ip { get; set; }
    }

    private class ShellySettings
    {

        public string? Type { get; set; }

        public string? Name { get; set; }
        public ShellyDevice? Device { get; set; }
    }
    private class ShellyDevice { public string? Hostname { get; set; } public string? Mac { get; set; } }


    private class ShellyInfo
    {

        public string? Type { get; set; }

        public string? Fw { get; set; }
        public string? Mac { get; set; }
    }
}