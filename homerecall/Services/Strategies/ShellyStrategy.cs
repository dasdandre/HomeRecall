namespace HomeRecall.Services;

public class ShellyStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Shelly;

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/settings");
        var files = new List<BackupFile> { new("settings.json", data) };

        string version = string.Empty;
        try
        {
            var info = await httpClient.GetFromJsonAsync<ShellySettings>($"http://{device.IpAddress}/shelly");
            if (info?.Fw != null) 
            {
                // Format: 20230913-112003/v1.14.0-gcb84623
                // We want to extract v1.14.0 if possible, but the full string is also fine.
                // Let's keep it simple: take what we get.
                version = info.Fw;
            }
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    private class ShellySettings { public string? Fw { get; set; } }
}