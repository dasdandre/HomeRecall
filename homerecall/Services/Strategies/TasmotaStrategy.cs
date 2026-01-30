namespace HomeRecall.Services;

public class TasmotaStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Tasmota;

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/dl");
        var files = new List<BackupFile> { new("Config.dmp", data) };

        string version = string.Empty;
        try
        {
            var statusJson = await httpClient.GetFromJsonAsync<TasmotaStatus>($"http://{device.IpAddress}/cm?cmnd=Status 2");
            if (statusJson?.StatusFWR?.Version != null)
            {
                // Format: 12.5.0(tasmota)
                version = statusJson.StatusFWR.Version;
            }
        }
        catch { /* Ignore version error */ }

        return new DeviceBackupResult(files, version);
    }

    private class TasmotaStatus
    {
        public StatusFwr? StatusFWR { get; set; }
    }
    private class StatusFwr
    {
        public string? Version { get; set; }
    }
}