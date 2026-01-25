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

    public BackupService(BackupContext context, IHttpClientFactory httpClientFactory, ILogger<BackupService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        
        // In HA Addon, backups should be in /backup or a mounted volume
        _backupDirectory = Environment.GetEnvironmentVariable("backup_path") ?? "/backup";
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
            
            byte[]? backupData = null;
            string fileName = "";

            // 1. Fetch Backup Data
            if (device.Type == DeviceType.Tasmota)
            {
                // Tasmota: http://ip/dl
                // This usually downloads Config.dmp
                backupData = await DownloadFileAsync($"http://{device.IpAddress}/dl");
                fileName = "Config.dmp";
            }
            else if (device.Type == DeviceType.Wled)
            {
                // WLED: http://ip/edit?download=cfg.json (approximate, needs verification of API)
                // Often /presets.json and /cfg.json are needed.
                // For simplicity, let's assume we fetch cfg.json
                backupData = await DownloadFileAsync($"http://{device.IpAddress}/edit?download=cfg.json");
                fileName = "cfg.json";
            }
            else if (device.Type == DeviceType.Shelly)
            {
                 // Shelly Gen1: http://ip/settings (json)
                 // Shelly Gen2: RPC calls. 
                 // This needs a more complex implementation. 
                 // Placeholder: fetch settings endpoint
                 backupData = await DownloadFileAsync($"http://{device.IpAddress}/settings");
                 fileName = "settings.json";
            }

            if (backupData == null || backupData.Length == 0)
            {
                _logger.LogError($"Failed to download backup data for {device.Name}");
                return;
            }

            // 2. Create ZIP in memory
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry(fileName);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(backupData);
            }
            
            memoryStream.Position = 0;
            byte[] zipBytes = memoryStream.ToArray();

            // 3. Compute SHA1
            using var sha1 = SHA1.Create();
            byte[] hashBytes = sha1.ComputeHash(zipBytes);
            string checksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // 4. Check Deduplication
            // Requirement: If checksum matches the LAST backup of THIS device, reuse file.
            
            var lastBackup = await _context.Backups
                .Where(b => b.DeviceId == device.Id)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            bool isDuplicate = lastBackup != null && lastBackup.Sha1Checksum == checksum;

            string storageFileName = $"{checksum}.zip";
            string storagePath = Path.Combine(_backupDirectory, storageFileName);
            
            // Just ensure we don't overwrite if it exists, or write if it doesn't.
            // Global deduplication of storage: If ANY backup has this hash, the file exists.
            if (!File.Exists(storagePath))
            {
                await File.WriteAllBytesAsync(storagePath, zipBytes);
            }

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

    private async Task<byte[]> DownloadFileAsync(string url)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        return await client.GetByteArrayAsync(url);
    }
}
