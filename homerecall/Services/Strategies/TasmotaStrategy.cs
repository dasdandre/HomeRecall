namespace HomeRecall.Services;

using System.Net.Http.Json;

public class TasmotaStrategy : IDeviceStrategy
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
                   

                    return new DiscoveredDevice
                    {
                        IpAddress = ip,
                        Type = DeviceType.Tasmota,
                        Name = name,
                        Hostname = hostname,
                        MacAddress = mac,
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

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
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

    // JSON Models
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
    private class StatusNet { public string? Hostname { get; set; } public string? Mac { get; set; } }
}