using System.Text;
using System.Security.Cryptography;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;

namespace HomeRecall.Services;

public interface IBackupService
{
    Task PerformBackupAsync(int deviceId);
}

public class BackupService : IBackupService
{
    private readonly BackupContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupDirectory;

    private record BackupFile(string Name, byte[] Content);

    public BackupService(BackupContext context, IHttpClientFactory httpClientFactory, ILogger<BackupService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // In HA Addon, backups should be in /backup or a mounted volume. Locally use ./backups
        _backupDirectory = Environment.GetEnvironmentVariable("backup_path") ?? Path.Combine(Directory.GetCurrentDirectory(), "backups");
        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
        }
    }

    public async Task PerformBackupAsync(int deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null) return;

        try
        {
            _logger.LogInformation($"Starting backup for {device.Name} ({device.Type})");

            var backupFiles = new List<BackupFile>();
            // 1. Fetch Backup Data
            if (device.Type == DeviceType.Tasmota)
            {
                // Tasmota: http://ip/dl
                // This usually downloads Config.dmp
                var data = await DownloadFileAsync($"http://{device.IpAddress}/dl");
                backupFiles.Add(new BackupFile("Config.dmp", data));
            }
            else if (device.Type == DeviceType.Wled)
            {
                // WLED: http://ip/edit?download=cfg.json
                var cfg = await DownloadFileAsync($"http://{device.IpAddress}/edit?download=cfg.json");
                backupFiles.Add(new BackupFile("cfg.json", cfg));

                // WLED often has presets as well
                try
                {
                    var presets = await DownloadFileAsync($"http://{device.IpAddress}/edit?download=presets.json");
                    backupFiles.Add(new BackupFile("presets.json", presets));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not download presets.json for {device.Name}: {ex.Message}");
                }
            }
            else if (device.Type == DeviceType.Shelly)
            {
                // Shelly Gen1: http://ip/settings (json)
                var data = await DownloadFileAsync($"http://{device.IpAddress}/settings");
                backupFiles.Add(new BackupFile("settings.json", data));
            }
            else if (device.Type == DeviceType.ShellyGen2)
            {
                 // Shelly Gen 2/3/4 (RPC based)
                 // Endpoint: http://ip/rpc/Shelly.GetConfig
                 // This returns the full configuration JSON.
                 var data = await DownloadFileAsync($"http://{device.IpAddress}/rpc/Shelly.GetConfig");
                 backupFiles.Add(new BackupFile("config.json", data));
                 
                 // Optionally, we could also fetch script content if scripts are used, 
                 // but GetConfig usually covers the main settings. 
                 // Scripts are stored separately in Gen2 usually, but for now Main Config is priority.
            }
            else if (device.Type == DeviceType.OpenDtu)
            {
                 // OpenDTU: http://ip/api/config
                 var data = await DownloadFileAsync($"http://{device.IpAddress}/api/config");
                 backupFiles.Add(new BackupFile("config.json", data));
                 
                 // Pin mapping is sometimes separate or included, checking documentation, config.json is the main export
            }
            else if (device.Type == DeviceType.AiOnTheEdge)
            {
                // AI-on-the-Edge-device
                // http://ip/fileserver/config/config.ini
                // http://ip/fileserver/config/ref0.jpg (etc) - Images are crucial.
                // Better approach: Some versions support a full zip export via API, 
                // but standard file access is reliable.
                
                // Let's try to get the config.ini first
                var configIni = await DownloadFileAsync($"http://{device.IpAddress}/fileserver/config/config.ini");
                backupFiles.Add(new BackupFile("config/config.ini", configIni));

                // Try to get Reference Images. Usually ref0.jpg, ref1.jpg...
                // We'll try a few common ones. 
                // A more robust way would be parsing config.ini to find used images, 
                // but for MVP we try standard paths.
                string[] potentialImages = { "config/ref0.jpg", "config/ref1.jpg", "config/reference.jpg" };
                
                foreach(var imgPath in potentialImages)
                {
                    try 
                    {
                        var imgData = await DownloadFileAsync($"http://{device.IpAddress}/fileserver/{imgPath}");
                        backupFiles.Add(new BackupFile(imgPath, imgData));
                    }
                    catch 
                    {
                        // Image might not exist, ignore
                    }
                }
            }
            else if (device.Type == DeviceType.Awtrix)
            {
                // Awtrix Light (Ulanzi TC001)
                // Access via /edit?download=config.json similar to WLED/LittleFS
                var config = await DownloadFileAsync($"http://{device.IpAddress}/edit?download=/config.json");
                backupFiles.Add(new BackupFile("config.json", config));
            }
            else if (device.Type == DeviceType.OpenHasp)
            {
                // openHASP
                // Uses LittleFS /edit endpoint too usually.
                // Essential files: config.json, pages.jsonl
                
                try 
                {
                    var config = await DownloadFileAsync($"http://{device.IpAddress}/edit?download=/config.json");
                    backupFiles.Add(new BackupFile("config.json", config));
                }
                catch (Exception ex) 
                {
                     _logger.LogWarning($"Could not download config.json for {device.Name}: {ex.Message}");
                }

                try
                {
                    var pages = await DownloadFileAsync($"http://{device.IpAddress}/edit?download=/pages.jsonl");
                    backupFiles.Add(new BackupFile("pages.jsonl", pages));
                }
                 catch (Exception ex) 
                {
                     _logger.LogWarning($"Could not download pages.jsonl for {device.Name}: {ex.Message}");
                }
            }

            if (backupFiles.Count == 0)
            {
                _logger.LogError($"No backup data retrieved for {device.Name}");
                return;
            }

            // 2. Sort files by name for determinism
            var sortedFiles = backupFiles.OrderBy(f => f.Name).ToList();

            // 3. Compute SHA1 based on raw content (Config Hash)
            // This ensures deduplication is based on the actual config, not the ZIP binary.
            string checksum = CalculateContentHash(sortedFiles);

            // 4. Create ZIP in memory
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in sortedFiles)
                {
                    var entry = archive.CreateEntry(file.Name);
                    // Fixed timestamp for additional ZIP-level determinism
                    entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(file.Content);
                }
            }

            memoryStream.Position = 0;
            byte[] zipBytes = memoryStream.ToArray();

            // 5. Check Deduplication (Information only)
            var lastBackup = await _context.Backups
                .Where(b => b.DeviceId == device.Id)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            bool isDuplicate = lastBackup != null && lastBackup.Sha1Checksum == checksum;

            // Generate readable filename
            // Sanitizing the name
            string safeName = string.Join("_", device.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            
            // Format: Date_Name_Type_Hash.zip
            // Putting date first allows chronological sorting in file explorer
            string storageFileName = $"{dateStr}_{safeName}_{device.Type}_{checksum[..8]}.zip";
            string storagePath = Path.Combine(_backupDirectory, storageFileName);

            // Always write the file as the timestamp makes it unique
            await File.WriteAllBytesAsync(storagePath, zipBytes);

            // Note: We always create a new DB entry to track the history event
            var backup = new Backup
            {
                DeviceId = device.Id,
                CreatedAt = DateTime.UtcNow,
                Sha1Checksum = checksum,
                StoragePath = storageFileName,
                IsLocked = false
            };

            _context.Backups.Add(backup);

            // Update Device LastBackup
            device.LastBackup = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Backup complete for {device.Name}. Deduplicated: {isDuplicate}");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error backing up {device.Name}");
        }
    }

    private string CalculateContentHash(List<BackupFile> files)
    {
        var allContent = files.SelectMany(f => f.Content).ToArray();
        return Convert.ToHexString(SHA1.HashData(allContent)).ToLowerInvariant();
    }

    private async Task<byte[]> DownloadFileAsync(string url)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        return await client.GetByteArrayAsync(url);
    }
}

