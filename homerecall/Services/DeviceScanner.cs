using System.Net;
using System.Net.NetworkInformation;

namespace HomeRecall.Services;

public class DeviceScanner : IDeviceScanner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeviceScanner> _logger;
    private readonly IEnumerable<IDeviceStrategy> _strategies;

    public DeviceScanner(
        IHttpClientFactory httpClientFactory, 
        ILogger<DeviceScanner> logger,
        IEnumerable<IDeviceStrategy> strategies)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _strategies = strategies;
    }

    public async Task<List<DiscoveredDevice>> ScanNetworkAsync(string startIp, string endIp, List<DeviceType> typesToScan, IProgress<ScanProgressReport>? progress = null)
    {
        var ipsToScan = GenerateIpsFromRange(startIp, endIp);
        var foundDevices = new System.Collections.Concurrent.ConcurrentBag<DiscoveredDevice>();

        // Parallelism
        var options = new ParallelOptions { MaxDegreeOfParallelism = 20 };
        int processedCount = 0;
        int total = ipsToScan.Count;
        
        await Parallel.ForEachAsync(ipsToScan, options, async (ip, token) =>
        {
             // For each IP, check requested strategies
             using var client = CreateClient();
             
             foreach (var strategy in _strategies)
             {
                 if (typesToScan.Contains(strategy.SupportedType))
                 {
                     var device = await strategy.ProbeAsync(ip, client);
                     if (device != null)
                     {
                         foundDevices.Add(device);
                         break; // Found matching type, move to next IP
                     }
                 }
             }
             
             // Report Progress
             int current = Interlocked.Increment(ref processedCount);
             if (progress != null && total > 0)
             {
                 if (current % 5 == 0 || current == total)
                 {
                     progress.Report(new ScanProgressReport((int)((double)current / total * 100), foundDevices.Count));
                 }
             }
        });

        return foundDevices.ToList();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(1.5);
        return client;
    }

    private List<string> GenerateIpsFromRange(string start, string end)
    {
        var ips = new List<string>();
        
        // We assume they are in same subnet for simplicity logic here, or just iterate.
        // Actually, converting to uint/long allows any range.
        
        if (System.Net.IPAddress.TryParse(start, out var ipStart) && System.Net.IPAddress.TryParse(end, out var ipEnd))
        {
            byte[] startBytes = ipStart.GetAddressBytes();
            byte[] endBytes = ipEnd.GetAddressBytes();

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(startBytes);
                Array.Reverse(endBytes);
            }

            uint startVal = BitConverter.ToUInt32(startBytes, 0);
            uint endVal = BitConverter.ToUInt32(endBytes, 0);

            if (endVal < startVal) return ips; // Invalid range

            // Safety limit: Don't allow massive scans > 500 IPs to prevent freezing
            if (endVal - startVal > 512) endVal = startVal + 512;

            for (uint i = startVal; i <= endVal; i++)
            {
                byte[] bytes = BitConverter.GetBytes(i);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                ips.Add(new System.Net.IPAddress(bytes).ToString());
            }
        }
        
        return ips;
    }
}