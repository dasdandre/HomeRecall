using System.Net.Http.Json;
using System.Text.Json;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Services.Strategies;

public class WledStrategy : IMqttDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Wled;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var info = await httpClient.GetFromJsonAsync<WledInfo>($"http://{ip}/json/info");
            if (info?.Ver != null && info.Leds != null)
            {
                string name = !string.IsNullOrWhiteSpace(info.Name) ? info.Name : $"WLED-{ip.Split('.').Last()}";
                
                    return new DiscoveredDevice
                    {
                        IpAddress = ip,
                        Type = DeviceType.Wled,
                        Name = name, 
                        MacAddress = info.Mac,
                        FirmwareVersion = info.Ver 
                    };
                }
            }
        catch {}
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();
        
        var cfg = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=cfg.json");
        files.Add(new("cfg.json", cfg));

        try
        {
            var presets = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=presets.json");
            files.Add(new("presets.json", presets));
        }
        catch { }

        string version = string.Empty;
        try
        {
            var info = await httpClient.GetFromJsonAsync<WledInfo>($"http://{device.IpAddress}/json/info");
            if (info?.Ver != null) version = info.Ver;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    public DiscoveredDevice? DiscoverFromMqtt(string topic, string payload)
    {
        // WLED version info on wled/+/v
        if (topic.StartsWith("wled/") && topic.EndsWith("/v"))
        {
            // Note: WLED default payloads don't always include IP,
            // might need further logic or matching.
        }
        return null;
    }

    public IEnumerable<string> MqttDiscoveryTopics => new[] { "wled/+/v" };

    public MqttDiscoveryMessage? DiscoveryMessage => null;

    private class WledInfo 
    { 
        public string? Ver { get; set; } 
        public string? Name { get; set; }
        public string? Mac { get; set; }
        public object? Leds { get; set; }
    }
}