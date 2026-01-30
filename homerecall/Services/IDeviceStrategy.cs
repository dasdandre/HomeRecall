namespace HomeRecall.Services;

public record DeviceBackupResult(List<BackupFile> Files, string FirmwareVersion);

public interface IDeviceStrategy
{
    DeviceType SupportedType { get; }
    Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient);
}