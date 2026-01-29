namespace HomeRecall.Services;

public class ShellyStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Shelly;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/settings");
        return new List<BackupFile> { new("settings.json", data) };
    }
}