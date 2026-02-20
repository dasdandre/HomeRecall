using System.ComponentModel.DataAnnotations;
using HomeRecall.Persistence.Enums;

namespace HomeRecall.Persistence.Entities;

public class Device
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string IpAddress { get; set; } = string.Empty;

    public string? Hostname { get; set; }

    private string? _macAddress;
    public string? MacAddress
    {
        get => _macAddress;
        set => _macAddress = HomeRecall.Utilities.NetworkUtils.NormalizeMac(value);
    }

    public string? HardwareModel { get; set; }

    public DeviceType Type { get; set; }

    public DateTime? LastBackup { get; set; }
    public string? CurrentFirmwareVersion { get; set; }

    // Auto-Backup specific overrides (optional, nullable means inherit from AppSettings)
    public bool? AutoBackupOverride { get; set; }

    public DateTime? LastAutoBackupAttempt { get; set; }

    public int BackupFailures { get; set; } = 0;

    public List<Backup> Backups { get; set; } = new();
}
