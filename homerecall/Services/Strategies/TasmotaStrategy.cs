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
            var status2 = await httpClient.GetFromJsonAsync<TasmotaStatus2>($"http://{ip}/cm?cmnd=Status 2");
            if (status2?.StatusFWR?.Version != null)
            {
                string defaultName = $"Tasmota-{ip.Split('.').Last()}";
                string name = defaultName;

                // Try to get Friendly Name / DeviceName / Hostname (heavier payload)
                try
                {
                    TasmotaStatusMain? status = null;
                    // Try encoded 'Status 0' first (common endpoint), then fallback to 'Status'
                    try
                    {
                        status = await httpClient.GetFromJsonAsync<TasmotaStatusMain>($"http://{ip}/cm?cmnd=Status%200");
                    }
                    catch
                    {
                        try
                        {
                            status = await httpClient.GetFromJsonAsync<TasmotaStatusMain>($"http://{ip}/cm?cmnd=Status");
                        }
                        catch { }
                    }

                    bool IsDefault(string? s) => string.IsNullOrWhiteSpace(s) || s.Equals("Tasmota", StringComparison.OrdinalIgnoreCase) || s.Equals("Tasmota2", StringComparison.OrdinalIgnoreCase);

                    if (status?.Status?.FriendlyName != null && status.Status.FriendlyName.Count > 0)
                    {
                        var first = status.Status.FriendlyName.ElementAtOrDefault(0);
                        var second = status.Status.FriendlyName.ElementAtOrDefault(1);
                        if (!IsDefault(first)) name = first!;
                        else if (!IsDefault(second)) name = second!;
                    }

                    if (name == defaultName)
                    {
                        if (!IsDefault(status?.Status?.DeviceName)) name = status?.Status?.DeviceName ?? name;
                    }

                    if (name == defaultName)
                    {
                        // try hostname if available in StatusNET
                        var host = status?.StatusNET?.Hostname ?? status?.Status?.Hostname;
                        if (!IsDefault(host)) name = host!;
                    }
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
        catch {}
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
    
    private class TasmotaStatusMain { public StatusInfo? Status { get; set; } public StatusNet? StatusNET { get; set; } }
    private class StatusInfo
    {
        public string? DeviceName { get; set; }
        public List<string>? FriendlyName { get; set; }
        public string? Hostname { get; set; }
    }
    private class StatusNet { public string? Hostname { get; set; } }
}