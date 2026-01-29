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
                // Shelly Gen2: RPC calls.
                // This needs a more complex implementation.
                // Placeholder: fetch settings endpoint
                var data = await DownloadFileAsync($"http://{device.IpAddress}/settings");
                backupFiles.Add(new BackupFile("settings.json", data));
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

            // 5. Check Deduplication
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

