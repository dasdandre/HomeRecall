namespace HomeRecall.Services;
using System.Net.Http.Json;

public class ShellyStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Shelly;

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
                                  
                    // Simple check if it's really Gen1 (Gen2 has different structure)
                    // If we are here, it worked.
                    return new DiscoveredDevice 
                    { 
                        IpAddress = ip, 
                        Type = DeviceType.Shelly, 
                        Name = name, 
                        FirmwareVersion = "Gen1" // Firmware version is in /status or /shelly usually
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
                        IpAddress = ip, 
                        Type = DeviceType.Shelly, 
                        Name = $"Shelly-Locked-{ip.Split('.').Last()}", 
                        FirmwareVersion = "Gen1" 
                    };
                }
            }
        }
        catch {}
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/settings");
        var files = new List<BackupFile> { new("settings.json", data) };

        string version = string.Empty;
        try
        {
            // Gen1 FW version is in /shelly
            var info = await httpClient.GetFromJsonAsync<ShellyInfo>($"http://{device.IpAddress}/shelly");
            if (info?.Fw != null) version = info.Fw;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    private class ShellySettings 
    { 
        public string? Type { get; set; } 
        public string? Name { get; set; }
        public ShellyDevice? Device { get; set; }
    }
    private class ShellyDevice { public string? Hostname { get; set; } }
    
    private class ShellyInfo 
    { 
        public string? Type { get; set; } 
        public string? Fw { get; set; }
    }
}