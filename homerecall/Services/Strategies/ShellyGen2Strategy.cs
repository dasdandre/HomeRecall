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
             var status = await httpClient.GetFromJsonAsync<ShellyStatus>($"http://{device.IpAddress}/rpc/Shelly.GetStatus");
             if (status?.Sys?.Ver != null) version = status.Sys.Ver;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }
    
    private class ShellyStatus { public ShellySys? Sys { get; set; } }
    private class ShellySys { public string? Ver { get; set; } }
}