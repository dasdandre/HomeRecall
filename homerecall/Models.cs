using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeRecall;

public enum DeviceType
{
    Tasmota,
    Wled,
    Shelly,
    ShellyGen2,
    OpenDtu,
    AiOnTheEdge,
    Awtrix,
    OpenHasp
}

public enum RetentionMode
{
    Smart,       // 24h full, 7d daily, 3m weekly
    SimpleDays,  // Keep all for X days
    SimpleCount, // Keep last X
    KeepAll      // Never delete
}

public class AppSettings
{
    public int Id { get; set; } // Singleton, usually ID 1
    
    // Auto-Backup Settings
    public bool AutoBackupEnabled { get; set; } = false;
    public int BackupIntervalHours { get; set; } = 24; // Default: Every 24h
    public int BackupStartHour { get; set; } = 3; // 03:00 AM
    
    // Retention / Pruning Settings
    public RetentionMode RetentionMode { get; set; } = RetentionMode.Smart;
    
    // For "SimpleDays" Mode
    public int MaxDaysToKeep { get; set; } = 30; 
    
    // For "SimpleCount" Mode
    public int MaxCountToKeep { get; set; } = 10;

    // UI Settings
    public bool UseRelativeTime { get; set; } = true;

    // Scan Settings Persistence
    public string? LastScanIpStart { get; set; }
    public int LastScanIpEndSuffix { get; set; } = 254;
    public string? LastScanDeviceTypes { get; set; } // Comma separated enum names
}

public class Device
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string IpAddress { get; set; } = string.Empty;

    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    
    public DeviceType Type { get; set; }
    
    public DateTime? LastBackup { get; set; }
    public string? CurrentFirmwareVersion { get; set; }
    
    // Auto-Backup specific overrides (optional, nullable means inherit from AppSettings)
    public bool? AutoBackupOverride { get; set; } 

        public DateTime? LastAutoBackupAttempt { get; set; }

    public int BackupFailures { get; set; } = 0;
    
    public List<Backup> Backups { get; set; } = new();
}

public class Backup
{
    public int Id { get; set; }
    
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; }
    
    [Required]
    public string Sha1Checksum { get; set; } = string.Empty;

    public string? FirmwareVersion { get; set; }
    
    // The filename stored on disk (likely just the checksum + .zip)
    public string StoragePath { get; set; } = string.Empty;
    
    public bool IsLocked { get; set; }
    
    public string? Note { get; set; }
}

public record BackupFile(string Name, byte[] Content);
