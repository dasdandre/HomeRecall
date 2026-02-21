namespace HomeRecall.Services.Strategies;

using System.Net.Http.Json;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class OpenDtuStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenDtu;

    private readonly ILogger<OpenDtuStrategy> _logger;

    public OpenDtuStrategy(ILogger<OpenDtuStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var status = await httpClient.GetFromJsonAsync<OpenDtuStatus>($"http://{ip}/api/system/status");
            if (status?.Hostname != null) // Version check is implicit
            {
                string name = !string.IsNullOrWhiteSpace(status.Hostname) ? status.Hostname : $"OpenDTU-{ip.Split('.').Last()}";


                return new DiscoveredDevice
                {

                    Type = DeviceType.OpenDtu,

                    Name = name,

                    FirmwareVersion = status.Version ?? "Detected",
                    Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Hostname = status.Hostname, Type = NetworkInterfaceType.Wifi } }
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
                var data = await httpClient.GetByteArrayAsync($"http://{ip}/api/config");
                var files = new List<BackupFile> { new("config.json", data) };
                _logger.LogTrace($"Successfully downloaded config.json from {ip} for {device.Name}.");

                string version = string.Empty;
                try
                {
                    var status = await httpClient.GetFromJsonAsync<OpenDtuStatus>($"http://{ip}/api/system/status");
                    if (status?.Version != null)
                    {
                        version = status.Version;
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


    private class OpenDtuStatus
    {

        public string? Version { get; set; }

        public string? Hostname { get; set; }
        // OpenDTU (at least recent versions) provides network info with MAC in /api/network/status or similar, 
        // but /api/system/status might not have it directly. 
        // We will stick to what's available for now, maybe add a network call if needed.
    }
}