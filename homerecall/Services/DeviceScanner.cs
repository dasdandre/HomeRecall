using System.Net;
using System.Net.NetworkInformation;
using System.Linq;

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

    public async Task<List<DiscoveredDevice>> ScanNetworkAsync(string startIp, string endIp, List<DeviceType> typesToScan, IProgress<ScanProgressReport>? progress = null, IEnumerable<string>? knownIps = null, CancellationToken cancellationToken = default)
    {
        var ipsToScan = GenerateIpsFromRange(startIp, endIp);
        var knownSet = knownIps != null ? new HashSet<string>(knownIps) : null;

        // Remove already-known IPs up-front to simplify scanning logic and progress calculation
        if (knownSet != null && knownSet.Count > 0)
        {
            ipsToScan = ipsToScan.Where(ip => !knownSet.Contains(ip)).ToList();
        }

        _logger.LogInformation("Starting network scan from {StartIp} to {EndIp} for types {Types} (knownIps={KnownCount}, ipsToScan={IpCount})", startIp, endIp, string.Join(',', typesToScan.Select(t => t.ToString())), knownSet?.Count ?? 0, ipsToScan.Count);
        var foundDevices = new System.Collections.Concurrent.ConcurrentBag<DiscoveredDevice>();

        // Parallelism
        var options = new ParallelOptions 
        { 
            MaxDegreeOfParallelism = 20, 
            CancellationToken = cancellationToken 
        };

        int processedCount = 0;
        // Use total = all ips to avoid progress > 100% when skipping known IPs
        int total = ipsToScan.Count;
        
        try
        {
            await Parallel.ForEachAsync(ipsToScan, options, async (ip, token) =>
            {
                // For each IP, check requested strategies
                using var client = CreateClient();
                DiscoveredDevice? foundDevice = null;
                
                foreach (var strategy in _strategies)
                {
                    if (token.IsCancellationRequested) break;

                    if (typesToScan.Contains(strategy.SupportedType))
                    {
                        var device = await strategy.ProbeAsync(ip, client);
                        if (device != null)
                        {
                            device.MacAddress = ServiceHelpers.NormalizeMac(device.MacAddress);                        
                            _logger.LogInformation("Found device {Type} at {Ip} ({Name})", device.Type, device.IpAddress, device.Name);
                            foundDevices.Add(device);
                            foundDevice = device;
                            break; // Found matching type, move to next IP
                        }
                    }
                }
                
                // Report Progress
                int current = Interlocked.Increment(ref processedCount);
                if (progress != null && total > 0)
                {
                    // Report immediately if found, otherwise throttle slightly
                    if (foundDevice != null || current % 5 == 0 || current == total)
                    {
                        progress.Report(new ScanProgressReport((int)((double)current / total * 100), foundDevices.Count, foundDevice));
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan cancelled by user.");
        }

        _logger.LogInformation("Scan completed: {FoundCount} devices found", foundDevices.Count);
        return foundDevices.ToList();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);
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