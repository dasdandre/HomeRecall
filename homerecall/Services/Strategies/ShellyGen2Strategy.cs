namespace HomeRecall.Services;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

public class ShellyGen2Strategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.ShellyGen2;

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var info = await httpClient.GetFromJsonAsync<ShellyDeviceInfo>($"http://{ip}/rpc/Shelly.GetDeviceInfo");
            if (info != null && (info.Gen == 2 || info.Gen == 3 || !string.IsNullOrEmpty(info.App)))
            {
                string name = !string.IsNullOrWhiteSpace(info.Name) ? info.Name : 
                              !string.IsNullOrWhiteSpace(info.Id) ? info.Id :
                              $"ShellyGen2-{ip.Split('.').Last()}";
                
                string version = info.Ver ?? info.FwId ?? "Gen2+";

                return new DiscoveredDevice 
                { 
                    IpAddress = ip, 
                    Type = DeviceType.ShellyGen2, 
                    Name = name, 
                    FirmwareVersion = version 
                };
            }
        }
        catch {}
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var data = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/rpc/Shelly.GetConfig");
        var files = new List<BackupFile> { new("config.json", data) };

        string version = string.Empty;
        try
        {
             var deviceInfo = await httpClient.GetFromJsonAsync<ShellyDeviceInfo>($"http://{device.IpAddress}/rpc/Shelly.GetDeviceInfo");
             if (deviceInfo?.Ver != null) version = deviceInfo.Ver;
             else if (deviceInfo?.FwId != null) version = deviceInfo.FwId;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }
    
    private class ShellyDeviceInfo 
    { 
        [JsonPropertyName("ver")] public string? Ver { get; set; }
        [JsonPropertyName("fw_id")] public string? FwId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; } // User name
        [JsonPropertyName("id")] public string? Id { get; set; } // Device ID
        [JsonPropertyName("app")] public string? App { get; set; } // Model
        [JsonPropertyName("gen")] public int? Gen { get; set; }
    }
}