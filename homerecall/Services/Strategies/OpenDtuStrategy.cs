namespace HomeRecall.Services;

public class OpenDtuStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenDtu;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/api/config");
        return new List<BackupFile> { new("config.json", data) };
    }
}