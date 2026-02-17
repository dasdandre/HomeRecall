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

    /// <summary>
    /// Gets the list of MQTT topics to subscribe to for discovery.
    /// </summary>
    IEnumerable<string> MqttDiscoveryTopics { get; }

    /// <summary>
    /// Gets the optional MQTT message to publish periodically to trigger device discovery.
    /// </summary>
    MqttDiscoveryMessage? DiscoveryMessage { get; }

    /// <summary>
    /// Attempts to discover a device from an incoming MQTT message.
    /// </summary>
    /// <param name="topic">The MQTT topic.</param>
    /// <param name="payload">The MQTT payload.</param>
    /// <returns>A <see cref="DiscoveredDevice"/> if the message identifies a supported device; otherwise, <c>null</c>.</returns>
    DiscoveredDevice? DiscoverFromMqtt(string topic, string payload);
}