using System.ComponentModel.DataAnnotations;

namespace HomeRecall.Persistence.Entities;

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

    public long BackupSize { get; set; } = 0;
    
    public bool IsLocked { get; set; }
    
    public string? Note { get; set; }
}
