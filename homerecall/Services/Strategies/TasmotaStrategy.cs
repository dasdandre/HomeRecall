namespace HomeRecall.Services;

public class TasmotaStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Tasmota;

    public async Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/dl");
        return new List<BackupFile> { new("Config.dmp", data) };
    }
}