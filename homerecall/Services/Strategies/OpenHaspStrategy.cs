using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

namespace HomeRecall.Services.Strategies;

public class OpenHaspStrategy : IMqttDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenHasp;

    private readonly ILogger<OpenHaspStrategy> _logger;

    public OpenHaspStrategy(ILogger<OpenHaspStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            using var response = await httpClient.GetAsync($"http://{ip}/api/info/");
            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<OpenHaspInfo>();
                if (info?.OpenHasp != null)
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.OpenHasp,
                        Name = $"OpenHasp-{ip.Split('.').Last()}",
                        FirmwareVersion = info.OpenHasp.Version ?? string.Empty,
                        HardwareModel = info.Module?.Model,
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, MacAddress = info.Wifi?.MacAddress, Type = NetworkInterfaceType.Wifi } }
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
                var files = new List<BackupFile>();
                var fileList = await httpClient.GetFromJsonAsync<List<OpenHaspFile>>($"http://{ip}/list?dir=/");
                if (fileList != null)
                {
                    _logger.LogTrace($"Found {fileList.Count} potential files to backup from {ip}.");
                    foreach (var file in fileList.Where(f => f.Type == "file" && !string.IsNullOrEmpty(f.Name)))
                    {
                        try
                        {
                            var content = await httpClient.GetByteArrayAsync($"http://{ip}/{file.Name}?download=true");
                            files.Add(new(file.Name!, content));
                            _logger.LogTrace($"Successfully downloaded {file.Name} from {ip}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, $"Failed to download file {file.Name} from {ip}. Skipping.");
                        }
                    }
                }

                if (files.Count == 0)
                {
                    // If no files could be downloaded, we might consider the backup failed for this interface unless there legitimately are 0 files.
                    // Given the fallback purpose, if we fail to get files completely, throwing an Exception makes the loop naturally continue.
                    // However, OpenHASP may just have no files. For safety we won't throw because getting fileList succeeded.
                    _logger.LogWarning($"No files were retrieved for {device.Name} from {ip}.");
                }

                string version = string.Empty;
                try
                {
                    using var response = await httpClient.GetAsync($"http://{ip}/api/info/");
                    if (response.IsSuccessStatusCode)
                    {
                        var info = await response.Content.ReadFromJsonAsync<OpenHaspInfo>();
                        if (info?.OpenHasp?.Version != null)
                        {
                            version = info.OpenHasp.Version;
                            _logger.LogTrace($"Retrieved firmware version {version} from {ip}.");
                        }
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
        // openHASP status/info
        if (topic.EndsWith("/status/info"))
        {
            try
            {
                var info = JsonSerializer.Deserialize<OpenHaspMqttInfo>(payload);
                if (info != null && !string.IsNullOrEmpty(info.Ip))
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.OpenHasp,
                        Name = string.IsNullOrEmpty(info.Node) ? $"OpenHasp-{info.Ip.Split('.').Last()}" : info.Node,
                        FirmwareVersion = info.Version ?? "",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = info.Ip, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch { }
        }
        return null;
    }

    public IEnumerable<string> MqttDiscoveryTopics => new[] { "+/status/info" };

    public MqttDiscoveryMessage? DiscoveryMessage => null;

    private class OpenHaspMqttInfo
    {
        [JsonPropertyName("node")] public string? Node { get; set; }
        [JsonPropertyName("ip")] public string? Ip { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
    }

    private class OpenHaspFile
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class OpenHaspInfo
    {
        [JsonPropertyName("openHASP")]
        public OpenHaspData? OpenHasp { get; set; }

        [JsonPropertyName("Wifi")]
        public WifiData? Wifi { get; set; }

        [JsonPropertyName("Module")]
        public ModuleData? Module { get; set; }
    }

    private class OpenHaspData
    {
        public string? Version { get; set; }
    }

    private class WifiData
    {
        [JsonPropertyName("MAC Address")]
        public string? MacAddress { get; set; }
    }

    private class ModuleData
    {
        public string? Model { get; set; }
    }
}