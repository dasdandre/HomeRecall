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
    private readonly IEnumerable<IDeviceStrategy> _strategies;
    private readonly string _backupDirectory;

    public BackupService(
        BackupContext context, 
        IHttpClientFactory httpClientFactory, 
        ILogger<BackupService> logger,
        IEnumerable<IDeviceStrategy> strategies)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _strategies = strategies;

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

            var strategy = _strategies.FirstOrDefault(s => s.SupportedType == device.Type);
            if (strategy == null)
            {
                _logger.LogError($"No backup strategy found for type {device.Type}");
                return;
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var result = await strategy.BackupAsync(device, httpClient);

            if (result == null || result.Files == null || result.Files.Count == 0)
            {
                _logger.LogError($"No backup data retrieved for {device.Name}");
                return;
            }

            if (!string.IsNullOrEmpty(result.FirmwareVersion))
            {
                device.CurrentFirmwareVersion = result.FirmwareVersion;
            }

            // 2. Sort files by name for determinism
            var sortedFiles = result.Files.OrderBy(f => f.Name).ToList();

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

            // 5. Deduplication & Storage Logic
            // Check if the LAST backup of THIS device has the same content.
            var lastBackup = await _context.Backups
                .Where(b => b.DeviceId == device.Id)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            string storageFileName;
            bool contentChanged = true;

            if (lastBackup != null && lastBackup.Sha1Checksum == checksum)
            {
                // Content is identical to the last backup. Reuse the existing file.
                storageFileName = lastBackup.StoragePath;
                contentChanged = false;
                
                _logger.LogInformation($"Content unchanged for {device.Name}. Reusing file: {storageFileName}");

                // Resilience: If the user manually deleted the file, recreate it to be safe.
                string fullPath = Path.Combine(_backupDirectory, storageFileName);
                if (!File.Exists(fullPath))
                {
                    await File.WriteAllBytesAsync(fullPath, zipBytes);
                }
            }
            else
            {
                // Content changed (or first backup). Create new file.
                string safeName = string.Join("_", device.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                string dateStr = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                
                // Format: Date_Name_Type_Hash.zip
                storageFileName = $"{dateStr}_{safeName}_{device.Type}_{checksum[..8]}.zip";
                string storagePath = Path.Combine(_backupDirectory, storageFileName);

                await File.WriteAllBytesAsync(storagePath, zipBytes);
            }

            // Create History Entry
            var backup = new Backup
            {
                DeviceId = device.Id,
                CreatedAt = DateTime.UtcNow,
                Sha1Checksum = checksum,
                StoragePath = storageFileName,
                IsLocked = false,
                FirmwareVersion = result.FirmwareVersion,
                BackupSize = zipBytes.Length
            };

            _context.Backups.Add(backup);
            device.LastBackup = DateTime.UtcNow;
            device.BackupFailures = 0; // Reset failures on success

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Backup complete for {device.Name}. New File: {contentChanged}");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error backing up {device.Name}");
            // Increment failures
            device.BackupFailures++;
            await _context.SaveChangesAsync();
            
            // Rethrow so UI can show error
            throw; 
        }
    }

    private string CalculateContentHash(List<BackupFile> files)
    {
        var allContent = files.SelectMany(f => f.Content).ToArray();
        return Convert.ToHexString(SHA1.HashData(allContent)).ToLowerInvariant();
    }
}

