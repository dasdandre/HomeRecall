namespace HomeRecall.Services;

public class WledStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Wled;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();
        
        var cfg = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=cfg.json");
        files.Add(new("cfg.json", cfg));

        try
        {
            var presets = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=presets.json");
            files.Add(new("presets.json", presets));
        }
        catch
        {
            // Ignore if presets don't exist
        }

        return files;
    }
}