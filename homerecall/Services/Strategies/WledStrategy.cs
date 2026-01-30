namespace HomeRecall.Services;

public class WledStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Wled;

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();
        
        var cfg = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=cfg.json");
        files.Add(new("cfg.json", cfg));

        try
        {
            var presets = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=presets.json");
            files.Add(new("presets.json", presets));
        }
        catch { }

        string version = string.Empty;
        try
        {
            var info = await httpClient.GetFromJsonAsync<WledInfo>($"http://{device.IpAddress}/json/info");
            if (info?.Ver != null) version = info.Ver;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    private class WledInfo { public string? Ver { get; set; } }
}