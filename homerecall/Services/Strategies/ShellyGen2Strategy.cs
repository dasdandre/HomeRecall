namespace HomeRecall.Services;

public class ShellyGen2Strategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.ShellyGen2;

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/rpc/Shelly.GetConfig");
        var files = new List<BackupFile> { new("config.json", data) };

        string version = string.Empty;
        try
        {
             // Shelly Gen2/3/4 Status RPC
             // Try GetDeviceInfo which is more reliable for version info
             var deviceInfo = await httpClient.GetFromJsonAsync<ShellyDeviceInfo>($"http://{device.IpAddress}/rpc/Shelly.GetDeviceInfo");
             if (deviceInfo?.Ver != null) 
             {
                 version = deviceInfo.Ver;
             }
             else if (deviceInfo?.FwId != null)
             {
                 // Fallback to fw_id if ver is missing
                 version = deviceInfo.FwId;
             }
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }
    
    private class ShellyDeviceInfo 
    { 
        [System.Text.Json.Serialization.JsonPropertyName("ver")]
        public string? Ver { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("fw_id")]
        public string? FwId { get; set; }
    }
}