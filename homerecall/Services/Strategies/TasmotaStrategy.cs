namespace HomeRecall.Services;
using System.Net.Http.Json;

public class TasmotaStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Tasmota;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            // First check: Status 2 (Firmware) - fast and standard
            var status2 = await httpClient.GetFromJsonAsync<TasmotaStatus2>($"http://{ip}/cm?cmnd=Status 2");
            if (status2?.StatusFWR?.Version != null)
            {
                string name = $"Tasmota-{ip.Split('.').Last()}";
                
                // Try to get Friendly Name (Status 0 or Status)
                // This is a heavier payload, so we do it only if Status 2 succeeded
                try 
                {
                    var status = await httpClient.GetFromJsonAsync<TasmotaStatusMain>($"http://{ip}/cm?cmnd=Status");
                    if (status?.Status?.DeviceName != null) name = status.Status.DeviceName;
                    else if (status?.Status?.FriendlyName != null && status.Status.FriendlyName.Count > 0) name = status.Status.FriendlyName[0];
                }
                catch {}

                return new DiscoveredDevice 
                { 
                    IpAddress = ip, 
                    Type = DeviceType.Tasmota, 
                    Name = name, 
                    FirmwareVersion = status2.StatusFWR.Version 
                };
            }
        }
        catch {}
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/dl");
        var files = new List<BackupFile> { new("Config.dmp", data) };

        string version = string.Empty;
        try
        {
            var statusJson = await httpClient.GetFromJsonAsync<TasmotaStatus2>($"http://{device.IpAddress}/cm?cmnd=Status 2");
            if (statusJson?.StatusFWR?.Version != null)
            {
                version = statusJson.StatusFWR.Version;
            }
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    // JSON Models
    private class TasmotaStatus2 { public StatusFwr? StatusFWR { get; set; } }
    private class StatusFwr { public string? Version { get; set; } }
    
    private class TasmotaStatusMain { public StatusInfo? Status { get; set; } }
    private class StatusInfo 
    { 
        public string? DeviceName { get; set; }
        public List<string>? FriendlyName { get; set; }
    }
}