using System.Text.RegularExpressions;
namespace HomeRecall.Services.Strategies;

using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class AiOnTheEdgeStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.AiOnTheEdge;

    private readonly ILogger<AiOnTheEdgeStrategy> _logger;

    public AiOnTheEdgeStrategy(ILogger<AiOnTheEdgeStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            // Prefer checking the device's public config which is a stronger indicator
            try
            {
                var cfgResp = await httpClient.GetAsync($"http://{ip}/fileserver/config/config.ini");
                if (cfgResp.IsSuccessStatusCode)
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.AiOnTheEdge,
                        Name = $"AiEdge-{ip.Split('.').Last()}",
                        // MAC address requires /api/system or parsing the Overview page
                        FirmwareVersion = "Detected",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch { }

            // Fallback: validate /api/version content instead of accepting any 2xx
            try
            {
                var verResp = await httpClient.GetAsync($"http://{ip}/api/version");
                if (verResp.IsSuccessStatusCode)
                {
                    var content = await verResp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        // Look for explicit markers or the full project name
                        if (Regex.IsMatch(content, @"\bai-?on-?the-?edge\b", RegexOptions.IgnoreCase) ||
                            content.Contains("AiOnTheEdge", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("AiEdge", StringComparison.OrdinalIgnoreCase))
                        {
                            return new DiscoveredDevice
                            {
                                Type = DeviceType.AiOnTheEdge,
                                Name = $"AiEdge-{ip.Split('.').Last()}",
                                FirmwareVersion = content.Trim(),
                                Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Type = NetworkInterfaceType.Wifi } }
                            };
                        }
                    }
                }
            }
            catch { }
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

                var configIni = await httpClient.GetByteArrayAsync($"http://{ip}/fileserver/config/config.ini");
                files.Add(new("config/config.ini", configIni));
                _logger.LogTrace($"Successfully downloaded config.ini from {ip} for {device.Name}.");

                string[] potentialImages = { "config/ref0.jpg", "config/ref1.jpg", "config/reference.jpg" };
                foreach (var imgPath in potentialImages)
                {
                    try
                    {
                        var imgData = await httpClient.GetByteArrayAsync($"http://{ip}/fileserver/{imgPath}");
                        files.Add(new(imgPath, imgData));
                        _logger.LogTrace($"Successfully downloaded {imgPath} from {ip} for {device.Name}.");
                    }
                    catch
                    {
                        // Ignore missing images
                    }
                }

                string version = string.Empty;
                try
                {
                    version = await httpClient.GetStringAsync($"http://{ip}/api/version");
                    version = version.Trim().Replace("\"", ""); // Cleanup if JSON string
                    _logger.LogTrace($"Retrieved firmware version {version} from {ip}.");
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
}