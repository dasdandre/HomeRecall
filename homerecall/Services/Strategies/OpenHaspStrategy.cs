using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

namespace HomeRecall.Services.Strategies;

public class OpenHaspStrategy : IMqttDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenHasp;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            using var response = await httpClient.GetAsync($"http://{ip}/api/info/");
            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<OpenHaspInfo>();
                if (info?.OpenHasp != null)
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.OpenHasp,
                        Name = $"OpenHasp-{ip.Split('.').Last()}",
                        FirmwareVersion = info.OpenHasp.Version ?? string.Empty,
                        HardwareModel = info.Module?.Model,
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, MacAddress = info.Wifi?.MacAddress, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
        }
        catch { }
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();
        var ip = device.Interfaces.FirstOrDefault()?.IpAddress;
        if (ip == null) return new DeviceBackupResult(files, string.Empty);

        try
        {
            var fileList = await httpClient.GetFromJsonAsync<List<OpenHaspFile>>($"http://{ip}/list?dir=/");
            if (fileList != null)
            {
                foreach (var file in fileList.Where(f => f.Type == "file" && !string.IsNullOrEmpty(f.Name)))
                {
                    try
                    {
                        var content = await httpClient.GetByteArrayAsync($"http://{ip}/{file.Name}?download=true");
                        files.Add(new(file.Name!, content));
                    }
                    catch { /* Skip individual file if it fails */ }
                }
            }
        }
        catch { }

        string version = string.Empty;
        try
        {
            using var response = await httpClient.GetAsync($"http://{ip}/api/info/");
            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<OpenHaspInfo>();
                if (info?.OpenHasp?.Version != null) version = info.OpenHasp.Version;
            }
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    public DiscoveredDevice? DiscoverFromMqtt(string topic, string payload)
    {
        // openHASP status/info
        if (topic.EndsWith("/status/info"))
        {
            try
            {
                var info = JsonSerializer.Deserialize<OpenHaspMqttInfo>(payload);
                if (info != null && !string.IsNullOrEmpty(info.Ip))
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.OpenHasp,
                        Name = string.IsNullOrEmpty(info.Node) ? $"OpenHasp-{info.Ip.Split('.').Last()}" : info.Node,
                        FirmwareVersion = info.Version ?? "",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = info.Ip, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch { }
        }
        return null;
    }

    public IEnumerable<string> MqttDiscoveryTopics => new[] { "+/status/info" };

    public MqttDiscoveryMessage? DiscoveryMessage => null;

    private class OpenHaspMqttInfo
    {
        [JsonPropertyName("node")] public string? Node { get; set; }
        [JsonPropertyName("ip")] public string? Ip { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
    }

    private class OpenHaspFile
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class OpenHaspInfo
    {
        [JsonPropertyName("openHASP")]
        public OpenHaspData? OpenHasp { get; set; }

        [JsonPropertyName("Wifi")]
        public WifiData? Wifi { get; set; }

        [JsonPropertyName("Module")]
        public ModuleData? Module { get; set; }
    }

    private class OpenHaspData
    {
        public string? Version { get; set; }
    }

    private class WifiData
    {
        [JsonPropertyName("MAC Address")]
        public string? MacAddress { get; set; }
    }

    private class ModuleData
    {
        public string? Model { get; set; }
    }
}