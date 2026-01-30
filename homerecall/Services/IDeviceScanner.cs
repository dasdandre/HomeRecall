namespace HomeRecall.Services;

public record ScanProgressReport(int Percent, int FoundCount);

public interface IDeviceScanner
{
    Task<List<DiscoveredDevice>> ScanNetworkAsync(string startIp, string endIp, List<DeviceType> typesToScan, IProgress<ScanProgressReport>? progress = null);
}