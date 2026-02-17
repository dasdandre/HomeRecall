namespace HomeRecall.Services.Strategies;

using HomeRecall.Services;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using System.Net.Http.Json;

public class OpenDtuStrategy : BaseDeviceStrategy
{
    public override DeviceType SupportedType => DeviceType.OpenDtu;

    public override async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var status = await httpClient.GetFromJsonAsync<OpenDtuStatus>($"http://{ip}/api/system/status");
            if (status?.Hostname != null) // Version check is implicit
            {
                string name = !string.IsNullOrWhiteSpace(status.Hostname) ? status.Hostname : $"OpenDTU-{ip.Split('.').Last()}";
                
                return new DiscoveredDevice 
                { 
                    IpAddress = ip, 
                    Type = DeviceType.OpenDtu, 
                    Name = name, 
                    FirmwareVersion = status.Version ?? "Detected"
                };
            }
        }
        catch {}
        return null;
    }

    public override async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/api/config");
        var files = new List<BackupFile> { new("config.json", data) };

        string version = string.Empty;
        try 
        {
             var status = await httpClient.GetFromJsonAsync<OpenDtuStatus>($"http://{device.IpAddress}/api/system/status");
             if (status?.Version != null) version = status.Version;
        }
        catch { }

        return new DeviceBackupResult(files, version);
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