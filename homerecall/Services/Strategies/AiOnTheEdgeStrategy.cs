using System.Text.RegularExpressions;
namespace HomeRecall.Services.Strategies;

using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class AiOnTheEdgeStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.AiOnTheEdge;

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
        var files = new List<BackupFile>();
        var ip = device.Interfaces.FirstOrDefault()?.IpAddress;
        if (ip == null) return new DeviceBackupResult(files, string.Empty);

        var configIni = await httpClient.GetByteArrayAsync($"http://{ip}/fileserver/config/config.ini");
        files.Add(new("config/config.ini", configIni));

        string[] potentialImages = { "config/ref0.jpg", "config/ref1.jpg", "config/reference.jpg" };
        foreach (var imgPath in potentialImages)
        {
            try

            {
                var imgData = await httpClient.GetByteArrayAsync($"http://{ip}/fileserver/{imgPath}");
                files.Add(new(imgPath, imgData));
            }
            catch

            {
                // Ignore missing images
            }
        }

        string version = string.Empty;
        // AI-on-the-Edge often has /html/version.txt or just parse config? 
        // Or /api/hello returns basic info?
        // Standard API endpoint seems to be /api/version in recent builds.
        try
        {
            version = await httpClient.GetStringAsync($"http://{ip}/api/version");
            version = version.Trim().Replace("\"", ""); // Cleanup if JSON string
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }
}