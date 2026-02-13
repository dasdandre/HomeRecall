namespace HomeRecall.Services.Strategies;

using HomeRecall.Services;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

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
                 var info = await httpClient.GetFromJsonAsync<OpenHaspInfo>($"http://{ip}/json");
                 if (info?.Version != null)
                 {
                     return new DiscoveredDevice 
                     { 
                         IpAddress = ip, 
                         Type = DeviceType.OpenHasp, 
                         Name = $"OpenHasp-{ip.Split('.').Last()}", 
                         MacAddress = info.Mac,
                         FirmwareVersion = info.Version 
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
    
    private class OpenHaspInfo { public string? Version { get; set; } public string? Mac { get; set; } }
}