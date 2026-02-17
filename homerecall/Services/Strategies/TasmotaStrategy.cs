namespace HomeRecall.Services.Strategies;

using HomeRecall.Services;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class TasmotaStrategy : BaseDeviceStrategy
{
    public override DeviceType SupportedType => DeviceType.Tasmota;

    public override async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
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
                        hostname= status?.StatusNET?.Hostname ?? status?.Status?.Hostname?? "";
                        mac = status?.StatusNET?.Mac ?? "";
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

                    return new DiscoveredDevice
                    {
                        IpAddress = ip,
                        Type = DeviceType.Tasmota,
                        Name = name,
                        Hostname = hostname,
                        MacAddress = mac,
                        HardwareModel = hardwareModel,
                        FirmwareVersion = status2.StatusFWR.Version
                    };
                }
                catch { }

                return new DiscoveredDevice
                {
                    IpAddress = ip,
                    Type = DeviceType.Tasmota,
                    Name = name,
                    FirmwareVersion = status2.StatusFWR.Version
                };
            }
        }
        catch { }
        return null;
    }

    public override async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/dl");
        var files = new List<BackupFile> { new("Config.dmp", data) };

        string version = string.Empty;
        try
        {
            var statusJson = await httpClient.GetFromJsonAsync<TasmotaStatus2>($"http://{device.IpAddress}/cm?cmnd=Status 2");
            if (statusJson?.StatusFWR?.Version != null)
            {
                version = statusJson.StatusFWR.Version;
            }
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    public override DiscoveredDevice? DiscoverFromMqtt(string topic, string payload)
    {        
        if (topic.EndsWith("/STATUS5"))
        {
            try
            {
                var info = JsonSerializer.Deserialize<MqttStatus5>(payload);
                if (info != null && info.StatusNET != null)
                {
                    var discoveredDevice = new DiscoveredDevice{ Type = DeviceType.Tasmota };

                    // try to get IP address from StatusNET WiFi interface
                    // if not found, try to get IP address from StatusNET Ethernet interface    
                    // todo: handle multiple interfaces
                    // todo: handle IPV6
                    if ((info?.StatusNET?.IPAddress??"") != "0.0.0.0")
                    {                        
                        discoveredDevice.IpAddress = info?.StatusNET?.IPAddress;
                        discoveredDevice.Hostname = info?.StatusNET?.Hostname;
                        discoveredDevice.MacAddress = info?.StatusNET?.Mac;
                    }else if (info?.StatusNET?.Ethernet != null && (info?.StatusNET?.Ethernet?.IPAddress??"") != "0.0.0.0" )
                    {
                        discoveredDevice.IpAddress = info?.StatusNET?.Ethernet?.IPAddress;
                        discoveredDevice.Hostname = info?.StatusNET?.Ethernet?.Hostname;                        
                        discoveredDevice.MacAddress = info?.StatusNET?.Ethernet?.Mac;
                    }     

                    return discoveredDevice;
                }
            }
            catch { }
        }
        return null;
    }


    public override IEnumerable<string> MqttDiscoveryTopics => new[] { "stat/+/STATUS5" };
    
    // Send command to group topic "tasmotas" to request Status 5 (Network) from all devices
    public override MqttDiscoveryMessage? DiscoveryMessage => new("cmnd/tasmotas/Status", "5");

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