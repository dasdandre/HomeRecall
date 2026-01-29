namespace HomeRecall.Services;

public class AwtrixStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Awtrix;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var config = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/config.json");
        return new List<BackupFile> { new("config.json", config) };
    }
}