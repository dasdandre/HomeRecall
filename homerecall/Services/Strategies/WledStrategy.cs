using System.Net.Http.Json;
using System.Text.Json;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Services.Strategies;

public class WledStrategy : IMqttDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Wled;

    private readonly ILogger<WledStrategy> _logger;

    public WledStrategy(ILogger<WledStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var info = await httpClient.GetFromJsonAsync<WledInfo>($"http://{ip}/json/info");
            if (info?.Ver != null && info.Leds != null)
            {
                string name = !string.IsNullOrWhiteSpace(info.Name) ? info.Name : $"WLED-{ip.Split('.').Last()}";

                return new DiscoveredDevice
                {
                    Type = DeviceType.Wled,
                    Name = name,
                    FirmwareVersion = info.Ver,
                    Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, MacAddress = info.Mac, Type = NetworkInterfaceType.Wifi } }
                };
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
                var files = new List<BackupFile>();

                var cfg = await httpClient.GetByteArrayAsync($"http://{ip}/edit?download=cfg.json");
                files.Add(new("cfg.json", cfg));
                _logger.LogTrace($"Successfully downloaded cfg.json from {ip} for {device.Name}.");

                try
                {
                    var presets = await httpClient.GetByteArrayAsync($"http://{ip}/edit?download=presets.json");
                    files.Add(new("presets.json", presets));
                    _logger.LogTrace($"Successfully downloaded presets.json from {ip} for {device.Name}.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Could not download presets.json from {ip} for {device.Name}. Appending cfg.json only.");
                }

                string version = string.Empty;
                try
                {
                    var info = await httpClient.GetFromJsonAsync<WledInfo>($"http://{ip}/json/info");
                    if (info?.Ver != null)
                    {
                        version = info.Ver;
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
        // WLED version info on wled/+/v
        if (topic.StartsWith("wled/") && topic.EndsWith("/v"))
        {
            // Note: WLED default payloads don't always include IP,
            // might need further logic or matching.
        }
        return null;
    }

    public IEnumerable<string> MqttDiscoveryTopics => new[] { "wled/+/v" };

    public MqttDiscoveryMessage? DiscoveryMessage => null;

    private class WledInfo
    {

        public string? Ver { get; set; }

        public string? Name { get; set; }
        public string? Mac { get; set; }
        public object? Leds { get; set; }
    }
}