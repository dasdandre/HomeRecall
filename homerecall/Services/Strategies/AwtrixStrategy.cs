namespace HomeRecall.Services.Strategies;

using HomeRecall.Services;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

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
                        // JS logic uses: trimLeadingSlash(joinPaths(dir, filename))
                        // The download endpoint seems to be just the path on the device based on JS: fetch(`${dir}${filename}`)
                        // Wait, the JS says: const source_file_url = `${dir}${filename}`;
                        // And fetch(source_file_url) which is relative to the root of the web server.
                        // So http://ip/config.json or http://ip/icons/icon.gif
                        
                        // BUT: The fallback used /edit?download=/config.json previously.
                        // Let's look at JS again: 
                        // const source_file_url = `${dir}${filename}`; -> /config.json
                        // const fileResponse = await fetch(source_file_url); -> GET http://ip/config.json
                        
                        // It seems AWTRIX serves files directly from root if they are in root?
                        // Or maybe via /edit?download=... 
                        
                        // The JS snippet uses `fetch(source_file_url)` where source_file_url is constructed as /path/to/file.
                        // Example: dir='/', filename='config.json' -> '/config.json'
                        // Example: dir='/icons/', filename='x.gif' -> '/icons/x.gif'
                        
                        // So we should try to download from http://{ip}{path}{entry.Name}
                        
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
                            // Try fallback via edit download if direct access fails? 
                            // JS doesn't do that, so we stick to direct access for now.
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
            // Directory listing failed or empty
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