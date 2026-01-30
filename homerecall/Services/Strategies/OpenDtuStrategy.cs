namespace HomeRecall.Services;

public class OpenDtuStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenDtu;

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
    
    private class OpenDtuStatus { public string? Version { get; set; } }
}