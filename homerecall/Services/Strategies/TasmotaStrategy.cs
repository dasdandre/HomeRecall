namespace HomeRecall.Services.Strategies;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class TasmotaStrategy : IMqttDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Tasmota;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            // First check: Status 2 (Firmware) - fast and standard
            var status2 = await httpClient.GetFromJsonAsync<TasmotaStatus2>($"http://{ip}/cm?cmnd=Status%202");
            if (status2?.StatusFWR?.Version != null)
            {
                string defaultName = $"Tasmota-{ip.Split('.').Last()}";
                string name = defaultName;
                string hostname = string.Empty;
                string mac = string.Empty;
                var interfaceType = NetworkInterfaceType.Wifi;
                List<string> candidateNames = new List<string>();

                // Try to get Friendly Name / DeviceName / Hostname (heavier payload)
                try
                {
                    TasmotaStatusMain? status = null;
                    try
                    {
                        status = await httpClient.GetFromJsonAsync<TasmotaStatusMain>($"http://{ip}/cm?cmnd=Status%200");

                        bool IsDefault(string? s) => string.IsNullOrWhiteSpace(s) || s.Equals("Tasmota", StringComparison.OrdinalIgnoreCase) || s.Equals("Tasmota2", StringComparison.OrdinalIgnoreCase);

                        // FriendlyName is the most user-friendly, but not always present.                    
                        if (status?.Status?.FriendlyName != null && status.Status.FriendlyName.Count > 0)
                        {
                            candidateNames.Add(status?.Status?.FriendlyName.ElementAtOrDefault(0) ?? "");
                            candidateNames.Add(status?.Status?.FriendlyName.ElementAtOrDefault(1) ?? "");
                        }

                        // DeviceName is often present but less user-friendly, still worth trying
                        candidateNames.Add(status?.Status?.DeviceName ?? "");

                        // Hostname from StatusNET is often more user-friendly than the one in Status, try it as well        
                        candidateNames.Add(status?.StatusNET?.Hostname ?? "");

                        // Topic is often the least user-friendly, but can be a fallback
                        candidateNames.Add(status?.Status?.Topic ?? "");

                        // Pick the first non-default, non-empty name
                        name = candidateNames.FirstOrDefault(n => !IsDefault(n)) ?? defaultName;

                        // read hostname and mac if available
                        hostname = status?.StatusNET?.Hostname ?? status?.Status?.Hostname ?? "";

                        // Try to get MAC from StatusNET (Wifi) first

                        mac = status?.StatusNET?.Mac ?? "";

                        // If Wifi MAC is missing or invalid, try Ethernet
                        if (string.IsNullOrEmpty(mac) || (status?.StatusNET?.IPAddress == "0.0.0.0" && status?.StatusNET?.Ethernet != null))
                        {
                            mac = status?.StatusNET?.Ethernet?.Mac ?? mac;
                            interfaceType = NetworkInterfaceType.Ethernet;
                            if (string.IsNullOrEmpty(hostname))
                            {
                                hostname = status?.StatusNET?.Ethernet?.Hostname ?? "";
                            }
                        }
                    }
                    catch
                    { }

                    // Try to get Hardware Model
                    string? hardwareModel = null;
                    try
                    {
                        var moduleResp = await httpClient.GetFromJsonAsync<TasmotaModuleResponse>($"http://{ip}/cm?cmnd=Module");
                        if (moduleResp?.Module != null && moduleResp.Module.Count > 0)
                        {
                            hardwareModel = moduleResp.Module.Values.FirstOrDefault();
                        }
                    }
                    catch { }

                    var discoveredDevice = new DiscoveredDevice
                    {
                        Type = DeviceType.Tasmota,
                        Name = name,
                        HardwareModel = hardwareModel,
                        FirmwareVersion = status2.StatusFWR.Version,
                        Interfaces = new List<NetworkInterface>()
                    };

                    if (status?.StatusNET != null && (status.StatusNET.IPAddress ?? "") != "0.0.0.0" && !string.IsNullOrEmpty(status.StatusNET.IPAddress))
                    {
                        discoveredDevice.Interfaces.Add(new NetworkInterface
                        {

                            IpAddress = status.StatusNET.IPAddress,

                            Hostname = status.StatusNET.Hostname,

                            MacAddress = status.StatusNET.Mac,
                            Type = NetworkInterfaceType.Wifi
                        });
                    }

                    if (status?.StatusNET?.Ethernet != null && (status.StatusNET.Ethernet.IPAddress ?? "") != "0.0.0.0" && !string.IsNullOrEmpty(status.StatusNET.Ethernet.IPAddress))
                    {
                        discoveredDevice.Interfaces.Add(new NetworkInterface
                        {

                            IpAddress = status.StatusNET.Ethernet.IPAddress,

                            Hostname = status.StatusNET.Ethernet.Hostname,

                            MacAddress = status.StatusNET.Ethernet.Mac,
                            Type = NetworkInterfaceType.Ethernet
                        });
                    }

                    // Fallback if neither was added but we have an IP from the request itself
                    if (discoveredDevice.Interfaces.Count == 0)
                    {
                        discoveredDevice.Interfaces.Add(new NetworkInterface
                        {

                            IpAddress = ip,

                            Hostname = hostname,

                            MacAddress = mac,
                            Type = interfaceType
                        });
                    }

                    return discoveredDevice;
                }
                catch { }

                return new DiscoveredDevice
                {
                    Type = DeviceType.Tasmota,
                    Name = name,
                    FirmwareVersion = status2.StatusFWR.Version,
                    Interfaces = new List<NetworkInterface> { new() { IpAddress = ip } }
                };
            }
        }
        catch { }
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        if (device.Interfaces == null || device.Interfaces.Count == 0)
            return new DeviceBackupResult(new List<BackupFile>(), string.Empty);

        // Prefer Ethernet first, then fallback to others
        var interfacesToTry = device.Interfaces
            .OrderByDescending(i => i.Type == NetworkInterfaceType.Ethernet)
            .ToList();

        foreach (var netInterface in interfacesToTry)
        {
            var ip = netInterface.IpAddress;
            if (string.IsNullOrEmpty(ip)) continue;

            try
            {
                var data = await httpClient.GetByteArrayAsync($"http://{ip}/dl");
                var files = new List<BackupFile> { new("Config.dmp", data) };

                string version = string.Empty;
                try
                {
                    var statusJson = await httpClient.GetFromJsonAsync<TasmotaStatus2>($"http://{ip}/cm?cmnd=Status 2");
                    if (statusJson?.StatusFWR?.Version != null)
                    {
                        version = statusJson.StatusFWR.Version;
                    }
                }
                catch { }

                // If we reach here, backup succeeded on this interface
                return new DeviceBackupResult(files, version);
            }
            catch
            {
                // Failed on this interface, continue to the next one
                continue;
            }
        }

        // Return empty result if all interfaces failed
        return new DeviceBackupResult(new List<BackupFile>(), string.Empty);
    }

    public DiscoveredDevice? DiscoverFromMqtt(string topic, string payload)
    {

        if (topic.EndsWith("/STATUS5"))
        {
            try
            {
                var info = JsonSerializer.Deserialize<MqttStatus5>(payload);
                if (info != null && info.StatusNET != null)
                {
                    var discoveredDevice = new DiscoveredDevice { Type = DeviceType.Tasmota };

                    if ((info?.StatusNET?.IPAddress ?? "") != "0.0.0.0" && !string.IsNullOrEmpty(info?.StatusNET?.IPAddress))
                    {
                        discoveredDevice.Interfaces.Add(new()
                        {
                            IpAddress = info.StatusNET.IPAddress,
                            Hostname = info.StatusNET.Hostname,
                            MacAddress = info.StatusNET.Mac,
                            Type = NetworkInterfaceType.Wifi
                        });
                    }


                    if (info?.StatusNET?.Ethernet != null && (info.StatusNET.Ethernet.IPAddress ?? "") != "0.0.0.0" && !string.IsNullOrEmpty(info.StatusNET.Ethernet.IPAddress))
                    {
                        discoveredDevice.Interfaces.Add(new()
                        {
                            IpAddress = info.StatusNET.Ethernet.IPAddress,
                            Hostname = info.StatusNET.Ethernet.Hostname,
                            MacAddress = info.StatusNET.Ethernet.Mac,
                            Type = NetworkInterfaceType.Ethernet
                        });
                    }

                    return discoveredDevice;
                }
            }
            catch { }
        }
        return null;
    }


    public IEnumerable<string> MqttDiscoveryTopics => new[] { "stat/+/STATUS5" };

    // Send command to group topic "tasmotas" to request Status 5 (Network) from all devices

    public MqttDiscoveryMessage? DiscoveryMessage => new("cmnd/tasmotas/STATUS", "5");

    // JSON Models
    private class TasmotaInfo
    {
        [JsonPropertyName("IPAddress")] public string? IpAddress { get; set; }
        [JsonPropertyName("Hostname")] public string? Hostname { get; set; }
        [JsonPropertyName("Mac")] public string? MacAddress { get; set; }
        [JsonPropertyName("Version")] public string? Version { get; set; }
    }

    private class TasmotaStatus2 { public StatusFwr? StatusFWR { get; set; } }
    private class StatusFwr { public string? Version { get; set; } }

    private class TasmotaStatusMain
    {
        public StatusInfo? Status { get; set; }
        public StatusNet? StatusNET { get; set; }
    }

    private class StatusInfo
    {
        public string? DeviceName { get; set; }
        public List<string>? FriendlyName { get; set; }
        public string? Hostname { get; set; }

        public string? Topic { get; set; }
    }

    private class StatusEthernet
    {

        public string? Hostname { get; set; }

        public string? Mac { get; set; }

        public string? IPAddress { get; set; }

    }

    private class StatusNet
    {

        public string? Hostname { get; set; }

        public string? Mac { get; set; }

        public string? IPAddress { get; set; }

        public StatusEthernet? Ethernet { get; set; }
    }


    private class TasmotaModuleResponse
    {
        public Dictionary<string, string>? Module { get; set; }
    }

    private class MqttStatus5
    {
        public StatusNet? StatusNET { get; set; }
    }
}