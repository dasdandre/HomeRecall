namespace HomeRecall.Services;

public class AwtrixStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Awtrix;

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var config = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/config.json");
        var files = new List<BackupFile> { new("config.json", config) };

        string version = string.Empty;
        try 
        {
             var stats = await httpClient.GetFromJsonAsync<AwtrixStats>($"http://{device.IpAddress}/api/stats");
             if (stats?.Version != null) version = stats.Version;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    private class AwtrixStats { public string? Version { get; set; } }
}