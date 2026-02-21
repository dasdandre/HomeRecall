namespace HomeRecall.Services.Strategies;

using System.Net.Http.Json;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;

public class AwtrixStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Awtrix;

    private readonly ILogger<AwtrixStrategy> _logger;

    public AwtrixStrategy(ILogger<AwtrixStrategy> logger)
    {
        _logger = logger;
    }

    private class AwtrixStats
    {
        public string? version { get; set; }
        public string? uid { get; set; }
    }

    private class AwtrixFileEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public async Task<DiscoveredDevice?> ProbeAsync(string ip, HttpClient httpClient)
    {
        try
        {
            var response = await httpClient.GetAsync($"http://{ip}/api/stats");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                if (json.Contains("\"version\"") && json.Contains("\"uid\""))
                {
                    var stats = System.Text.Json.JsonSerializer.Deserialize<AwtrixStats>(json);

                    return new DiscoveredDevice
                    {
                        Type = DeviceType.Awtrix,
                        Name = stats?.uid != null ? $"Awtrix-{stats.uid}" : $"Awtrix-{ip.Split('.').Last()}",

                        FirmwareVersion = stats?.version ?? string.Empty,
                        Interfaces = new List<NetworkInterface> { new() { IpAddress = ip, Type = NetworkInterfaceType.Wifi } }
                    };
                }
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
                var files = new List<BackupFile>();
                await ScanDirectoryAsync(ip, "/", files, httpClient);

                if (files.Count == 0)
                {
                    _logger.LogTrace($"ScanDirectoryAsync yielded no files. Trying fallback to /config.json for {device.Name} at {ip}.");
                    var config = await httpClient.GetByteArrayAsync($"http://{ip}/edit?download=/config.json");
                    files.Add(new("config.json", config));
                }

                if (files.Count > 0)
                {
                    _logger.LogTrace($"Successfully retrieved {files.Count} files from {ip} for {device.Name}.");
                }
                else
                {
                    throw new Exception("No files could be retrieved from device.");
                }

                string version = string.Empty;
                try
                {
                    var stats = await httpClient.GetFromJsonAsync<AwtrixStats>($"http://{ip}/api/stats");
                    if (stats?.version != null)
                    {
                        version = stats.version;
                        _logger.LogTrace($"Retrieved firmware version {version} from {ip}.");
                    }
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

    private async Task ScanDirectoryAsync(string ip, string path, List<BackupFile> files, HttpClient httpClient)
    {
        try
        {
            var listUrl = $"http://{ip}/list?dir={path}";
            var entries = await httpClient.GetFromJsonAsync<List<AwtrixFileEntry>>(listUrl);

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry.Type == "file")
                    {
                        try

                        {
                            var fileUrl = $"http://{ip}{path}{entry.Name}";
                            var data = await httpClient.GetByteArrayAsync(fileUrl);

                            // Store with relative path (remove leading slash)

                            string storedName = $"{path}{entry.Name}".TrimStart('/');
                            files.Add(new BackupFile(storedName, data));
                        }
                        catch

                        {
                        }
                    }
                    else if (entry.Type == "dir")
                    {
                        await ScanDirectoryAsync(ip, $"{path}{entry.Name}/", files, httpClient);
                    }
                }
            }
        }
        catch

        {
        }
    }
}