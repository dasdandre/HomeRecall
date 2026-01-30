namespace HomeRecall.Services;

public class OpenHaspStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenHasp;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var response = await httpClient.GetAsync($"http://{ip}/json");
            if (response.IsSuccessStatusCode)
            {
                 var json = await response.Content.ReadAsStringAsync();
                 if (json.Contains("\"version\"") && json.Contains("\"model\""))
                 {
                     return new DiscoveredDevice 
                     { 
                         IpAddress = ip, 
                         Type = DeviceType.OpenHasp, 
                         Name = $"OpenHasp-{ip.Split('.').Last()}", 
                         FirmwareVersion = "Detected" 
                     };
                 }
            }
        }
        catch {}
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();

        try 
        {
            var config = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/config.json");
            files.Add(new("config.json", config));
        }
        catch { /* Log if necessary via ILogger injection, or just ignore optional files */ }

        try
        {
            var pages = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/pages.jsonl");
            files.Add(new("pages.jsonl", pages));
        }
        catch { }
        
        string version = string.Empty;
        // openHASP does not have a simple version endpoint in every build.
        // But /json returns system info.
        try 
        {
             var info = await httpClient.GetFromJsonAsync<OpenHaspInfo>($"http://{device.IpAddress}/json");
             if (info?.Version != null) version = info.Version;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }
    
    private class OpenHaspInfo { public string? Version { get; set; } }
}