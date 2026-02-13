using HomeRecall.Persistence.Enums;

namespace HomeRecall.Services;

public record ScanProgressReport(int Percent, int FoundCount, DiscoveredDevice? LatestDevice = null);

public interface IDeviceScanner
{
    Task<List<DiscoveredDevice>> ScanNetworkAsync(string startIp, string endIp, List<DeviceType> typesToScan, IProgress<ScanProgressReport>? progress = null, IEnumerable<string>? knownIps = null, CancellationToken cancellationToken = default);
}
