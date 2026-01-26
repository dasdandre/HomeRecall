using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HomeRecall;

namespace HomeRecall.Controllers;

public class BackupsController : Controller
{
    private readonly BackupContext _context;
    private readonly string _backupDirectory;

    public BackupsController(BackupContext context)
    {
        _context = context;
        _backupDirectory = Environment.GetEnvironmentVariable("backup_path") ?? "/backup";
    }

    [Route("Backups/Device/{deviceId}")]
    public async Task<IActionResult> Index(int deviceId)
    {
        var device = await _context.Devices
            .Include(d => d.Backups)
            .FirstOrDefaultAsync(d => d.Id == deviceId);
            
        if (device == null) return NotFound();
        
        return View(device);
    }
    
    [HttpPost]
    [Route("Backups/Download/{backupId}")]
    public async Task<IActionResult> Download(int backupId)
    {
        var backup = await _context.Backups.FindAsync(backupId);
        if (backup == null) return NotFound();
        
        var path = Path.Combine(_backupDirectory, backup.StoragePath);
        if (!System.IO.File.Exists(path)) return NotFound("Backup file missing from disk.");
        
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, "application/zip", $"{backup.Sha1Checksum}.zip");
    }

    [HttpPost]
    [Route("Backups/ToggleLock/{backupId}")]
    public async Task<IActionResult> ToggleLock(int backupId)
    {
        var backup = await _context.Backups.FindAsync(backupId);
        if (backup == null) return NotFound();

        backup.IsLocked = !backup.IsLocked;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { deviceId = backup.DeviceId });
    }

    [HttpPost]
    [Route("Backups/UpdateNote/{backupId}")]
    public async Task<IActionResult> UpdateNote(int backupId, string note)
    {
        var backup = await _context.Backups.FindAsync(backupId);
        if (backup == null) return NotFound();

        backup.Note = note;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { deviceId = backup.DeviceId });
    }

    [HttpPost]
    [Route("Backups/Delete/{backupId}")]
    public async Task<IActionResult> Delete(int backupId)
    {
        var backup = await _context.Backups.FindAsync(backupId);
        if (backup == null) return NotFound();

        if (backup.IsLocked)
        {
             // Could add an error message here, but for now just redirect
             return RedirectToAction(nameof(Index), new { deviceId = backup.DeviceId });
        }

        // Check if other backups use the same file
        var otherBackupsUsingFile = await _context.Backups
            .AnyAsync(b => b.StoragePath == backup.StoragePath && b.Id != backup.Id);

        if (!otherBackupsUsingFile)
        {
            var path = Path.Combine(_backupDirectory, backup.StoragePath);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        _context.Backups.Remove(backup);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { deviceId = backup.DeviceId });
    }
}
