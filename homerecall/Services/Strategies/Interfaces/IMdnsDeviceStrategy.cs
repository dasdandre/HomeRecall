namespace HomeRecall.Services.Strategies;

using HomeRecall.Persistence.Entities;
using Makaretu.Dns;

public interface IMdnsDeviceStrategy : IDeviceStrategy
{
    /// <summary>
    /// Gets a list of mDNS service types (e.g. "_tasmota._tcp.local") that this strategy supports.
    /// </summary>
    IEnumerable<string> MdnsServiceTypes { get; }

    /// <summary>
    /// Attempts to parse an mDNS discovery event into a DiscoveredDevice.
    /// Returns null if the payload does not belong to this device type.
    /// </summary>
    /// <param name="eventArgs">The mDNS service discovery event arguments</param>
    /// <returns>A DiscoveredDevice or null</returns>
    DiscoveredDevice? DiscoverFromMdns(MessageEventArgs eventArgs);
}
