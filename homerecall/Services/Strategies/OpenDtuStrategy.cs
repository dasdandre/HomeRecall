namespace HomeRecall.Services;
using System.Net.Http.Json;

public class OpenDtuStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenDtu;

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

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
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
    }
}