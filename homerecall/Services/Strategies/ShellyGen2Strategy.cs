namespace HomeRecall.Services;

public class ShellyGen2Strategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.ShellyGen2;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/rpc/Shelly.GetConfig");
        return new List<BackupFile> { new("config.json", data) };
    }
}