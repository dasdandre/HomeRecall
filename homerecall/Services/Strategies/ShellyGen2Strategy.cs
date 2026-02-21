namespace HomeRecall.Services.Strategies;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class ShellyGen2Strategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.ShellyGen2;

    private readonly ILogger<ShellyGen2Strategy> _logger;

    public ShellyGen2Strategy(ILogger<ShellyGen2Strategy> logger)
    {
        _logger = logger;
    }

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

                    Type = DeviceType.ShellyGen2,

                    Name = name,

                    FirmwareVersion = version,
                    HardwareModel = info.App,
                    Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, MacAddress = info.Mac, Type = NetworkInterfaceType.Wifi } }
                };
            }
        }
        catch { }
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        if (device.Interfaces == null || device.Interfaces.Count == 0)
        {
            _logger.LogWarning($"No interfaces found for {device.Name} during backup.");
            return new DeviceBackupResult(new List<BackupFile>(), string.Empty);
        }

        var interfacesToTry = device.Interfaces
            .OrderByDescending(i => i.Type == NetworkInterfaceType.Ethernet)
            .ToList();

        _logger.LogDebug($"Attempting backup for {device.Name} across {interfacesToTry.Count} interfaces. Preference: Ethernet first.");

        foreach (var netInterface in interfacesToTry)
        {
            var ip = netInterface.IpAddress;
            if (string.IsNullOrEmpty(ip)) continue;

            _logger.LogTrace($"Trying to backup {device.Name} using interface IP {ip} ({netInterface.Type})...");

            try
            {
                var data = await httpClient.GetByteArrayAsync($"http://{ip}/rpc/Shelly.GetConfig");
                var files = new List<BackupFile> { new("config.json", data) };
                _logger.LogTrace($"Successfully downloaded config.json from {ip} for {device.Name}.");

                try
                {
                    var scriptList = await httpClient.GetFromJsonAsync<ShellyScriptListResponse>($"http://{ip}/rpc/Script.List");
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
                                        $"http://{ip}/rpc/Script.GetCode?id={script.Id}&offset={offset}");

                                    if (codeResp?.Data != null)
                                    {
                                        sb.Append(codeResp.Data);
                                        offset += System.Text.Encoding.UTF8.GetByteCount(codeResp.Data);
                                    }

                                    if (codeResp == null || codeResp.Remaining <= 0) break;
                                }

                                if (sb.Length > 0)
                                {
                                    string safeName = script.Name ?? "";
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
                                    _logger.LogTrace($"Successfully downloaded script {finalName} from {ip}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, $"Failed to download script {script.Id} from {ip}.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Could not retrieve scripts list from {ip} for {device.Name}.");
                }

                string version = string.Empty;
                try
                {
                    var deviceInfo = await httpClient.GetFromJsonAsync<ShellyDeviceInfo>($"http://{ip}/rpc/Shelly.GetDeviceInfo");
                    if (deviceInfo?.Ver != null) version = deviceInfo.Ver;
                    else if (deviceInfo?.FwId != null) version = deviceInfo.FwId;


                    _logger.LogTrace($"Retrieved firmware version {version} from {ip}.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Could not retrieve firmware version from {ip} for {device.Name}.");
                }

                _logger.LogDebug($"Backup for {device.Name} succeeded using interface IP {ip} ({netInterface.Type}).");
                return new DeviceBackupResult(files, version);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Backup failed for {device.Name} on interface IP {ip} ({netInterface.Type}). Falling back to next interface if available...");
                continue;
            }
        }

        _logger.LogWarning($"Backup failed for all interfaces of {device.Name}.");
        return new DeviceBackupResult(new List<BackupFile>(), string.Empty);
    }

    // JSON models

    private class ShellyDeviceInfo
    {

        [JsonPropertyName("ver")] public string? Ver { get; set; }
        [JsonPropertyName("fw_id")] public string? FwId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("app")] public string? App { get; set; }
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