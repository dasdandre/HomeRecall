using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeRecall;

public enum DeviceType
{
    Tasmota,
    Wled,
    Shelly
}

public class Device
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string IpAddress { get; set; } = string.Empty;
    
    public DeviceType Type { get; set; }
    
    public DateTime? LastBackup { get; set; }
    
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
    
    // The filename stored on disk (likely just the checksum + .zip)
    public string StoragePath { get; set; } = string.Empty;
    
    public bool IsLocked { get; set; }
    
    public string? Note { get; set; }
}
