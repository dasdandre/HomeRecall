using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HomeRecall.Services;

public class BackupScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupScheduler> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 minutes if a job is due

    public BackupScheduler(IServiceProvider serviceProvider, ILogger<BackupScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup Scheduler Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunBackups(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Backup Scheduler");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndRunBackups(CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BackupContext>();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        // 1. Load Settings
        var settings = await context.Settings.FindAsync(1);
        if (settings == null || !settings.AutoBackupEnabled)
        {
            return;
        }

        // 2. Check if we are in the correct time window (simple approach first)
        // For "Every X Hours", we check per device when it was last backed up.
        // For simplicity in MVP: We just check if (Now - LastBackup) > Interval
        
        var interval = TimeSpan.FromHours(settings.BackupIntervalHours);
        
        var devices = await context.Devices.ToListAsync(token);

        foreach (var device in devices)
        {
            if (device.AutoBackupOverride == false) continue; // Disabled for this device

            var lastBackupTime = device.LastBackup ?? DateTime.MinValue;
            var nextDueTime = lastBackupTime.Add(interval);

            if (DateTime.UtcNow >= nextDueTime)
            {
                _logger.LogInformation($"Auto-Backup triggered for {device.Name} (Last: {lastBackupTime})");
                
                try
                {
                    await backupService.PerformBackupAsync(device.Id);
                    
                    // Cleanup / Retention
                    await ApplyRetentionPolicy(context, device.Id, settings);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Auto-Backup failed for {device.Name}");
                }
            }
        }
    }

    private async Task ApplyRetentionPolicy(BackupContext context, int deviceId, AppSettings settings)
    {
        // Don't prune if mode is KeepAll
        if (settings.RetentionMode == RetentionMode.KeepAll) return;

        var backups = await context.Backups
            .Where(b => b.DeviceId == deviceId && !b.IsLocked) // Never delete locked ones
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var toDelete = new List<Backup>();

        if (settings.RetentionMode == RetentionMode.SimpleCount)
        {
            // Keep last X
            if (backups.Count > settings.MaxCountToKeep)
            {
                toDelete = backups.Skip(settings.MaxCountToKeep).ToList();
            }
        }
        else if (settings.RetentionMode == RetentionMode.SimpleDays)
        {
            // Keep all within X days
            var cutoff = DateTime.UtcNow.AddDays(-settings.MaxDaysToKeep);
            toDelete = backups.Where(b => b.CreatedAt < cutoff).ToList();
        }
        else if (settings.RetentionMode == RetentionMode.Smart)
        {
            // Smart GFS (24h full, 7d daily, 3m weekly)
            // Implementation: We iterate through backups and decide which ones to KEEP.
            // The rest goes to delete list.
            
            var keepList = new HashSet<int>();
            var now = DateTime.UtcNow;

            // 1. Keep ALL in last 24h
            var last24h = backups.Where(b => b.CreatedAt >= now.AddHours(-24)).ToList();
            foreach (var b in last24h) keepList.Add(b.Id);

            // 2. Keep ONE per day for last 7 days
            // We group by Date, take the LAST one of that day.
            var last7Days = backups
                .Where(b => b.CreatedAt < now.AddHours(-24) && b.CreatedAt >= now.AddDays(-7))
                .GroupBy(b => b.CreatedAt.Date)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();
            
            foreach (var b in last7Days) keepList.Add(b.Id);

            // 3. Keep ONE per week for last 3 months (90 days)
            // We group by "Year-Week", take the LAST one.
            var last90Days = backups
                .Where(b => b.CreatedAt < now.AddDays(-7) && b.CreatedAt >= now.AddDays(-90))
                .GroupBy(b => System.Globalization.ISOWeek.GetWeekOfYear(b.CreatedAt))
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();

            foreach (var b in last90Days) keepList.Add(b.Id);
            
            // 4. Always keep the very last 5 backups regardless of age (Safety net)
            foreach(var b in backups.Take(5)) keepList.Add(b.Id);

            // Calculate deletion list
            toDelete = backups.Where(b => !keepList.Contains(b.Id)).ToList();
        }

        if (toDelete.Any())
        {
            _logger.LogInformation($"Retention ({settings.RetentionMode}): Deleting {toDelete.Count} old backups for Device {deviceId}");
            
            foreach (var backup in toDelete)
            {
                // Check physical file deletion
                var otherBackupsUsingFile = await context.Backups
                    .AnyAsync(b => b.StoragePath == backup.StoragePath && b.Id != backup.Id);

                if (!otherBackupsUsingFile)
                {
                    // In a real scheduler, we need to be careful about file paths.
                    // We assume env var is set or default.
                    var backupDir = Environment.GetEnvironmentVariable("backup_path") ?? Path.Combine(Directory.GetCurrentDirectory(), "backups");
                    var path = Path.Combine(backupDir, backup.StoragePath);
                    if (File.Exists(path)) File.Delete(path);
                }

                context.Backups.Remove(backup);
            }
            await context.SaveChangesAsync();
        }
    }
}