using System.Net;
using System.Net.NetworkInformation;

namespace HomeRecall.Services;

public class DiscoveredDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
}

public interface IDeviceScanner
{
    Task<List<DiscoveredDevice>> ScanNetworkAsync(string startIp, string endIp, List<DeviceType> typesToScan, IProgress<int>? progress = null);
}

public class DeviceScanner : IDeviceScanner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeviceScanner> _logger;

    public DeviceScanner(IHttpClientFactory httpClientFactory, ILogger<DeviceScanner> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<DiscoveredDevice>> ScanNetworkAsync(string startIp, string endIp, List<DeviceType> typesToScan, IProgress<int>? progress = null)
    {
        var ipsToScan = GenerateIpsFromRange(startIp, endIp);
        var foundDevices = new System.Collections.Concurrent.ConcurrentBag<DiscoveredDevice>();

        // Parallelism
        var options = new ParallelOptions { MaxDegreeOfParallelism = 20 };
        int processedCount = 0;
        int total = ipsToScan.Count;
        
        await Parallel.ForEachAsync(ipsToScan, options, async (ip, token) =>
        {
             var result = await ProbeDeviceAsync(ip, typesToScan);
             if (result != null)
             {
                 foundDevices.Add(result);
             }
             
             // Report Progress
             int current = Interlocked.Increment(ref processedCount);
             if (progress != null && total > 0)
             {
                 // Report only every few items to reduce UI thread load
                 if (current % 5 == 0 || current == total)
                 {
                     progress.Report((int)((double)current / total * 100));
                 }
             }
        });

        return foundDevices.ToList();
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

    private async Task<DiscoveredDevice?> ProbeDeviceAsync(string ip, List<DeviceType> types)
    {
        // Short timeout is critical for scanning speed
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(1.5); 

        try
        {
            // Try endpoints. Order matters slightly for efficiency.

            if (types.Contains(DeviceType.Tasmota))
            {
                try 
                {
                    var response = await client.GetAsync($"http://{ip}/cm?cmnd=Status 2");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (json.Contains("StatusFWR"))
                        {
                             // Extract Name? Maybe later.
                             return new DiscoveredDevice 
                             { 
                                 IpAddress = ip, 
                                 Type = DeviceType.Tasmota, 
                                 Name = $"Tasmota-{ip.Split('.').Last()}", 
                                 FirmwareVersion = "Detected" 
                             };
                        }
                    }
                }
                catch {}
            }

            if (types.Contains(DeviceType.ShellyGen2))
            {
                try
                {
                    var response = await client.GetAsync($"http://{ip}/rpc/Shelly.GetDeviceInfo");
                    if (response.IsSuccessStatusCode)
                    {
                         var json = await response.Content.ReadAsStringAsync();
                         // Gen2+ usually returns valid JSON with "app" or "gen"
                         if (json.Contains("\"app\"") || json.Contains("\"gen\""))
                         {
                             return new DiscoveredDevice 
                             { 
                                 IpAddress = ip, 
                                 Type = DeviceType.ShellyGen2, 
                                 Name = $"ShellyGen2-{ip.Split('.').Last()}", 
                                 FirmwareVersion = "Gen2+" 
                             };
                         }
                    }
                }
                catch {}
            }

            if (types.Contains(DeviceType.Shelly))
            {
                try
                {
                    var response = await client.GetAsync($"http://{ip}/shelly");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        // Gen1 check
                        if (json.Contains("\"type\"") && !json.Contains("\"gen\":2"))
                        {
                            return new DiscoveredDevice 
                            { 
                                IpAddress = ip, 
                                Type = DeviceType.Shelly, 
                                Name = $"Shelly-{ip.Split('.').Last()}", 
                                FirmwareVersion = "Gen1" 
                            };
                        }
                    }
                }
                catch {}
            }

            if (types.Contains(DeviceType.Wled))
            {
                try
                {
                    var response = await client.GetAsync($"http://{ip}/json/info");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (json.Contains("\"ver\"") && json.Contains("\"leds\""))
                        {
                            return new DiscoveredDevice 
                            { 
                                IpAddress = ip, 
                                Type = DeviceType.Wled, 
                                Name = $"WLED-{ip.Split('.').Last()}", 
                                FirmwareVersion = "Detected" 
                            };
                        }
                    }
                }
                catch {}
            }

            if (types.Contains(DeviceType.OpenDtu))
            {
                try
                {
                    var response = await client.GetAsync($"http://{ip}/api/system/status");
                    if (response.IsSuccessStatusCode)
                    {
                         var json = await response.Content.ReadAsStringAsync();
                         if (json.Contains("\"hostname\"") && json.Contains("\"version\""))
                         {
                             return new DiscoveredDevice 
                             { 
                                 IpAddress = ip, 
                                 Type = DeviceType.OpenDtu, 
                                 Name = $"OpenDTU-{ip.Split('.').Last()}", 
                                 FirmwareVersion = "Detected" 
                             };
                         }
                    }
                }
                catch {}
            }
            
            // Add checks for other types (AiOnTheEdge, Awtrix, OpenHasp) similarly...
            if (types.Contains(DeviceType.AiOnTheEdge))
            {
                try
                {
                    // /api/version or /fileserver/config/config.ini existence
                    var response = await client.GetAsync($"http://{ip}/api/version");
                    if (response.IsSuccessStatusCode)
                    {
                        return new DiscoveredDevice 
                        { 
                            IpAddress = ip, 
                            Type = DeviceType.AiOnTheEdge, 
                            Name = $"AiEdge-{ip.Split('.').Last()}", 
                            FirmwareVersion = "Detected" 
                        };
                    }
                }
                catch {}
            }

            if (types.Contains(DeviceType.Awtrix))
            {
                try
                {
                    var response = await client.GetAsync($"http://{ip}/api/stats");
                    if (response.IsSuccessStatusCode)
                    {
                         var json = await response.Content.ReadAsStringAsync();
                         if (json.Contains("\"version\"") && json.Contains("\"bat\"")) // Battery/Stats
                         {
                             return new DiscoveredDevice 
                             { 
                                 IpAddress = ip, 
                                 Type = DeviceType.Awtrix, 
                                 Name = $"Awtrix-{ip.Split('.').Last()}", 
                                 FirmwareVersion = "Detected" 
                             };
                         }
                    }
                }
                catch {}
            }

            if (types.Contains(DeviceType.OpenHasp))
            {
                try
                {
                    var response = await client.GetAsync($"http://{ip}/json");
                    if (response.IsSuccessStatusCode)
                    {
                         var json = await response.Content.ReadAsStringAsync();
                         if (json.Contains("\"version\"") && json.Contains("\"model\""))
                         {
                             return new DiscoveredDevice 
                             { 
                                 IpAddress = ip, 
                                 Type = DeviceType.OpenHasp, 
                                 Name = $"OpenHasp-{ip.Split('.').Last()}", 
                                 FirmwareVersion = "Detected" 
                             };
                         }
                    }
                }
                catch {}
            }

        }
        catch 
        {
            // General connection error to IP (timeout/refused)
        }

        return null;
    }
}