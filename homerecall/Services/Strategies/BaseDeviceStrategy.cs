using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Services.Strategies;

/// <summary>
/// Base class for device strategies, providing default implementations for optional methods.
/// </summary>
public abstract class BaseDeviceStrategy : IDeviceStrategy
{
    /// <inheritdoc />
    public abstract DeviceType SupportedType { get; }

    /// <inheritdoc />
    public abstract Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient);        

    /// <inheritdoc />
    public abstract Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient);

    /// <inheritdoc />
    /// <remarks>
    /// Default implementation returns null. Override this if the strategy supports MQTT discovery.
    /// </remarks>
    public virtual DiscoveredDevice? DiscoverFromMqtt(string topic, string payload) => null;

    /// <inheritdoc />
    /// <remarks>
    /// Default implementation returns an empty list. Override this if the strategy supports MQTT discovery.
    /// </remarks>
    public virtual IEnumerable<string> MqttDiscoveryTopics => Enumerable.Empty<string>();

    /// <inheritdoc />
    /// <remarks>
    /// Default implementation returns null. Override this if the strategy needs to send a periodic discovery message.
    /// </remarks>
    public virtual MqttDiscoveryMessage? DiscoveryMessage => null;
}
