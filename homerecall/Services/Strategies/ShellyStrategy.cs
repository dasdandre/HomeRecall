using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

namespace HomeRecall.Services.Strategies;

public class ShellyStrategy : IMqttDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Shelly;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            // Gen1 uses /settings for Name, but it might be protected.
            // /shelly is public.

            // Try /settings first (Auth might fail)
            try

            {
                var settings = await httpClient.GetFromJsonAsync<ShellySettings>($"http://{ip}/settings");
                if (settings?.Type != null)
                {
                    string name = !string.IsNullOrWhiteSpace(settings.Name) ? settings.Name :

                                  !string.IsNullOrWhiteSpace(settings.Device?.Hostname) ? settings.Device.Hostname :

                                  $"Shelly-{ip.Split('.').Last()}";


                    return new DiscoveredDevice
                    {

                        Type = DeviceType.Shelly,

                        Name = name,

                        FirmwareVersion = "Gen1",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Hostname = settings.Device?.Hostname, MacAddress = settings.Device?.Mac, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Auth required. Fallback to /shelly (public)
                var info = await httpClient.GetFromJsonAsync<ShellyInfo>($"http://{ip}/shelly");
                if (info?.Type != null)
                {
                    return new DiscoveredDevice
                    {

                        Type = DeviceType.Shelly,

                        Name = $"Shelly-Locked-{ip.Split('.').Last()}",

                        FirmwareVersion = "Gen1",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, MacAddress = info.Mac, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
        }
        catch { }
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var ip = device.Interfaces.FirstOrDefault()?.IpAddress;
        if (ip == null) return new DeviceBackupResult(new List<BackupFile>(), string.Empty);

        var data = await httpClient.GetByteArrayAsync($"http://{ip}/settings");
        var files = new List<BackupFile> { new("settings.json", data) };

        string version = string.Empty;
        try
        {
            var info = await httpClient.GetFromJsonAsync<ShellyInfo>($"http://{ip}/shelly");
            if (info?.Fw != null) version = info.Fw;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    public DiscoveredDevice? DiscoverFromMqtt(string topic, string payload)
    {
        // Shelly Gen 1 announces on shelly/+/announce
        if (topic.StartsWith("shelly/") && topic.EndsWith("/announce"))
        {
            try
            {
                var announce = JsonSerializer.Deserialize<ShellyAnnounce>(payload);
                if (announce != null && !string.IsNullOrEmpty(announce.Ip))
                {
                    return new DiscoveredDevice
                    {
                        Type = DeviceType.Shelly,
                        Name = $"Shelly-{announce.Ip.Split('.').Last()}",
                        FirmwareVersion = "Gen1",
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = announce.Ip, MacAddress = announce.Mac, Type = NetworkInterfaceType.Wifi } }
                    };
                }
            }
            catch { }
        }
        return null;
    }

    public IEnumerable<string> MqttDiscoveryTopics => new[] { "shelly/+/announce" };

    public MqttDiscoveryMessage? DiscoveryMessage => null;

    private class ShellyAnnounce
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("mac")] public string? Mac { get; set; }
        [JsonPropertyName("ip")] public string? Ip { get; set; }
    }

    private class ShellySettings
    {

        public string? Type { get; set; }

        public string? Name { get; set; }
        public ShellyDevice? Device { get; set; }
    }
    private class ShellyDevice { public string? Hostname { get; set; } public string? Mac { get; set; } }


    private class ShellyInfo
    {

        public string? Type { get; set; }

        public string? Fw { get; set; }
        public string? Mac { get; set; }
    }
}