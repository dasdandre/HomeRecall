using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Services.Strategies;

public class DiscoveredDevice
{
    public string Name { get; set; } = string.Empty;
    public string? HardwareModel { get; set; }
    public DeviceType Type { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
    public List<NetworkInterface> Interfaces { get; set; } = new();
}

public record DeviceBackupResult(List<BackupFile> Files, string FirmwareVersion);

public record MqttDiscoveryMessage(string Topic, string Payload);

/// <summary>
/// Defines the contract for device interaction strategies.
/// Each strategy supports a specific device type and handles discovery, probing, and backup.
/// </summary>
public interface IDeviceStrategy
{
    /// <summary>
    /// Gets the type of device this strategy supports.
    /// </summary>
    DeviceType SupportedType { get; }

    /// <summary>
    /// Probes an IP address to check if it matches the supported device type.
    /// </summary>
    /// <param name="ip">The IP address to probe.</param>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <returns>A <see cref="DiscoveredDevice"/> if the IP belongs to a supported device; otherwise, <c>null</c>.</returns>
    Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient);

    /// <summary>
    /// Performs a backup of the specified device.
    /// </summary>
    /// <param name="device">The device to backup.</param>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <returns>The result of the backup operation containing files and version info.</returns>
    Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient);


}