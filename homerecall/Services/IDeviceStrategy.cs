namespace HomeRecall.Services;

public interface IDeviceStrategy
{
    DeviceType SupportedType { get; }
    Task<List<BackupFile>> BackupAsync(Device device, HttpClient httpClient);
}