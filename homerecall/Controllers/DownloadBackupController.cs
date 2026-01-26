using Microsoft.AspNetCore.Mvc;
using HomeRecall;

namespace HomeRecall.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DownloadBackupController : ControllerBase
{
    private readonly BackupContext _context;
    private readonly string _backupDirectory;

    public DownloadBackupController(BackupContext context)
    {
        _context = context;
        _backupDirectory = Environment.GetEnvironmentVariable("backup_path") ?? "/backup";
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var backup = await _context.Backups.FindAsync(id);
        if (backup == null) return NotFound();
        
        var path = Path.Combine(_backupDirectory, backup.StoragePath);
        if (!System.IO.File.Exists(path)) return NotFound("Backup file missing from disk.");
        
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, "application/zip", $"{backup.Sha1Checksum}.zip");
    }
}
