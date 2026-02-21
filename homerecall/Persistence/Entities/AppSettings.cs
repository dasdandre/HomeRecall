using HomeRecall.Persistence.Enums;

namespace HomeRecall.Persistence.Entities;

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
    public bool MdnsEnabled { get; set; } = true;

    // MQTT Settings
    public bool MqttEnabled { get; set; } = false;
    public string? MqttHost { get; set; }
    public int MqttPort { get; set; } = 1883;
    public string? MqttUsername { get; set; }
    public string? MqttPasswordEncrypted { get; set; }
    public bool MqttAutoAdd { get; set; } = false;
    public string MqttExcludedDeviceTypes { get; set; } = string.Empty;
}
