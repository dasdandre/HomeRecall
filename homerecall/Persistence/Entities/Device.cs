using System.ComponentModel.DataAnnotations;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Persistence.Entities;

public class Device
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public List<NetworkInterface> Interfaces { get; set; } = new();

    public string? HardwareModel { get; set; }

    public DeviceType Type { get; set; }

    public DateTime? LastBackup { get; set; }
    public string? CurrentFirmwareVersion { get; set; }

    // Auto-Backup specific overrides (optional, nullable means inherit from AppSettings)
    public bool? AutoBackupOverride { get; set; }

    public DateTime? LastAutoBackupAttempt { get; set; }

    public int BackupFailures { get; set; } = 0;

    public List<Backup> Backups { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DeviceSource Source { get; set; } = DeviceSource.Manual;
}
