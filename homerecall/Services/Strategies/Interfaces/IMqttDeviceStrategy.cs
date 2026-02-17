using HomeRecall.Persistence.Entities;

namespace HomeRecall.Services.Strategies;

/// <summary>
/// Defines the contract for device strategies that support MQTT discovery.
/// </summary>
public interface IMqttDeviceStrategy : IDeviceStrategy
{
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
