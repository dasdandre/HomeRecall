namespace HomeRecall.Services.Strategies;

using HomeRecall.Services;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using System.Net.Http.Json;

public class AwtrixStrategy : IDeviceStrategy
{
    public DeviceType SupportedType => DeviceType.Awtrix;

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
                         IpAddress = ip, 
                         Type = DeviceType.Awtrix, 
                         Name = stats?.uid ?? $"Awtrix-{ip.Split('.').Last()}",
                         
                         FirmwareVersion = stats?.version ?? string.Empty                          
                     };
                 }
            }
        }
        catch {}
        return null;
    }

    public async Task<DeviceBackupResult> BackupAsync(Device device, HttpClient httpClient)
    {
        var files = new List<BackupFile>();
        await ScanDirectoryAsync(device.IpAddress, "/", files, httpClient);
        
        // If files is empty, try fallback to just config.json to not break existing behavior completely
        if (files.Count == 0)
        {
             try 
             {
                var config = await httpClient.GetByteArrayAsync($"http://{device.IpAddress}/edit?download=/config.json");
                files.Add(new("config.json", config));
             }
             catch {}
        }

        string version = string.Empty;
        try 
        {
             var stats = await httpClient.GetFromJsonAsync<AwtrixStats>($"http://{device.IpAddress}/api/stats");
             if (stats?.version != null) 
             version = stats.version;
        }
        catch { }

        return new DeviceBackupResult(files, version);
    }

    public DiscoveredDevice? DiscoverFromMqtt(string topic, string payload) => null;
    public IEnumerable<string> MqttDiscoveryTopics => Enumerable.Empty<string>();

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
                        string downloadPath = $"{path}{entry.Name}";
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

    private class AwtrixFileEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    private class AwtrixStats { 
        public string? version { get; set; }
        public string? uid { get; set; }
     }
}