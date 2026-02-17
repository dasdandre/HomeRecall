using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Services;

public class DiscoveredDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? HardwareModel { get; set; }
    public DeviceType Type { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
}

public record DeviceBackupResult(List<BackupFile> Files, string FirmwareVersion);

public interface IDeviceStrategy
{
    DeviceType SupportedType { get; }
    Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient);
    Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient);
    DiscoveredDevice? DiscoverFromMqtt(string topic, string payload);
    IEnumerable<string> MqttDiscoveryTopics { get; }
}