using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;

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

            // Progressive Retry Logic
            // If backup is overdue, check how often we should retry
            if (DateTime.UtcNow >= nextDueTime)
            {
                // Backup is due. Now check retry policy.
                var lastAttempt = device.LastAutoBackupAttempt ?? DateTime.MinValue;
                var timeSinceLastBackup = DateTime.UtcNow - lastBackupTime;
                
                // If offline for > 3 days -> Retry only once per day
                // Else -> Retry normally (every scheduler tick / 15 mins)
                TimeSpan retryInterval = timeSinceLastBackup.TotalDays > 3 
                    ? TimeSpan.FromHours(24) 
                    : TimeSpan.FromMinutes(15);

                if (DateTime.UtcNow >= lastAttempt.Add(retryInterval))
                {
                    _logger.LogInformation($"Auto-Backup triggered for {device.Name} (Last Success: {lastBackupTime}, Last Attempt: {lastAttempt})");
                    
                    // Mark attempt time immediately
                    device.LastAutoBackupAttempt = DateTime.UtcNow;
                    // We need to save this even if backup fails, to respect retry interval
                    // But we can save it after the attempt in the catch block or here.
                    // Saving here means if process crashes, we wait. Saving after means if crash, we retry immediately.
                    // Let's save after to be safe, but we need a way to persist it.
                    // Since we are in a loop, we can't easily save just the device without tracking.
                    // EF Core tracks it. We just need to call SaveChanges at the end or per device.
                    
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
                    finally 
                    {
                         // Save the attempt timestamp
                         await context.SaveChangesAsync(token);
                    }
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
            // Group by Date, take the LAST one of that day.
            var oneWeekAgo = now.AddDays(-7);
            var last7Days = backups
                .Where(b => b.CreatedAt < now.AddHours(-24) && b.CreatedAt >= oneWeekAgo)
                .GroupBy(b => b.CreatedAt.Date)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();
            
            foreach (var b in last7Days) keepList.Add(b.Id);

            // 3. Keep ONE per week for last 1 month
            // We group by "Year-Week", take the LAST one.
            var oneMonthAgo = now.AddMonths(-1);
            var lastMonth = backups
                .Where(b => b.CreatedAt < oneWeekAgo && b.CreatedAt >= oneMonthAgo)
                .GroupBy(b => System.Globalization.ISOWeek.GetWeekOfYear(b.CreatedAt))
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();

            foreach (var b in lastMonth) keepList.Add(b.Id);
            
            // 4. Keep ONE per month for last 12 months (1 year)
            var oneYearAgo = now.AddYears(-1);
            var lastYear = backups
                .Where(b => b.CreatedAt < oneMonthAgo && b.CreatedAt >= oneYearAgo)
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();

            foreach (var b in lastYear) keepList.Add(b.Id);

            // 5. Keep ONE per year forever (older than 1 year)
            var olderYears = backups
                .Where(b => b.CreatedAt < oneYearAgo)
                .GroupBy(b => b.CreatedAt.Year)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();

            foreach (var b in olderYears) keepList.Add(b.Id);

            // 6. Always keep the very last 5 backups regardless of age (Safety net)
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