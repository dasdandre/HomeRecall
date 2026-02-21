namespace HomeRecall.Services.Strategies;

using System.Net.Http.Json;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class OpenDtuStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.OpenDtu;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var status = await httpClient.GetFromJsonAsync<OpenDtuStatus>($"http://{ip}/api/system/status");
            if (status?.Hostname != null) // Version check is implicit
            {
                string name = !string.IsNullOrWhiteSpace(status.Hostname) ? status.Hostname : $"OpenDTU-{ip.Split('.').Last()}";


                return new DiscoveredDevice
                {

                    Type = DeviceType.OpenDtu,

                    Name = name,

                    FirmwareVersion = status.Version ?? "Detected",
                    Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Hostname = status.Hostname, Type = NetworkInterfaceType.Wifi } }
                };
            }
        }
        catch { }
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var ip = device.Interfaces.FirstOrDefault()?.IpAddress;
        if (ip == null) return new DeviceBackupResult(new List<BackupFile>(), string.Empty);

        var data = await httpClient.GetByteArrayAsync($"http://{ip}/api/config");
        var files = new List<BackupFile> { new("config.json", data) };

        string version = string.Empty;
        try
        {
            var status = await httpClient.GetFromJsonAsync<OpenDtuStatus>($"http://{ip}/api/system/status");
            if (status?.Version != null) version = status.Version;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }


    private class OpenDtuStatus
    {

        public string? Version { get; set; }

        public string? Hostname { get; set; }
        // OpenDTU (at least recent versions) provides network info with MAC in /api/network/status or similar, 
        // but /api/system/status might not have it directly. 
        // We will stick to what's available for now, maybe add a network call if needed.
    }
}