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
                    MacAddress = info.Mac,
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

        try
        {
            var scriptList = await httpClient.GetFromJsonAsync<ShellyScriptListResponse>($"http://{device.IpAddress}/rpc/Script.List");
            if (scriptList?.Scripts != null)
            {
                foreach (var script in scriptList.Scripts)
                {
                    try
                    {
                        var sb = new System.Text.StringBuilder();
                        int offset = 0;
                        while (true)
                        {
                            var codeResp = await httpClient.GetFromJsonAsync<ShellyScriptCodeResponse>(
                                $"http://{device.IpAddress}/rpc/Script.GetCode?id={script.Id}&offset={offset}");

                            if (codeResp?.Data != null)
                            {
                                sb.Append(codeResp.Data);
                                offset += System.Text.Encoding.UTF8.GetByteCount(codeResp.Data);
                            }

                            if (codeResp == null || codeResp.Remaining <= 0) break;
                        }

                        if (sb.Length > 0)
                        {
                            string safeName = script.Name;
                            if (string.IsNullOrWhiteSpace(safeName))
                            {
                                safeName = $"script_{script.Id}";
                            }
                            else
                            {
                                foreach (var c in Path.GetInvalidFileNameChars())
                                {
                                    safeName = safeName.Replace(c, '_');
                                }
                            }

                            if (!safeName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                                safeName += ".js";

                            string finalName = $"scripts/{safeName}";
                            // Ensure uniqueness
                            if (files.Any(f => f.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)))
                            {
                                finalName = $"scripts/{Path.GetFileNameWithoutExtension(safeName)}_{script.Id}.js";
                            }

                            files.Add(new BackupFile(finalName, System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

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
        [JsonPropertyName("mac")] public string? Mac { get; set; }
        [JsonPropertyName("gen")] public int? Gen { get; set; }
    }

    private class ShellyScriptListResponse
    {
        [JsonPropertyName("scripts")] public List<ShellyScript>? Scripts { get; set; }
    }

    private class ShellyScript
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private class ShellyScriptCodeResponse
    {
        [JsonPropertyName("data")] public string? Data { get; set; }
        [JsonPropertyName("remaining")] public int Remaining { get; set; }
    }
}