namespace HomeRecall.Services;

public class DiscoveredDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
}

public record DeviceBackupResult(List<BackupFile> Files, string FirmwareVersion);

public interface IDeviceStrategy
{
    DeviceType SupportedType { get; }
    Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient);
    Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient);
}