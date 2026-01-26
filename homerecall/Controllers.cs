using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HomeRecall;

namespace HomeRecall.Controllers;

public class HomeController : Controller
{
    private readonly BackupContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly Services.IBackupService _backupService;

    public HomeController(BackupContext context, ILogger<HomeController> logger, Services.IBackupService backupService)
    {
        _context = context;
        _logger = logger;
        _backupService = backupService;
    }

    public async Task<IActionResult> Index()
    {
        var devices = await _context.Devices
            .Include(d => d.Backups)
            .ToListAsync();
        return View(devices);
    }
    
    [HttpPost]
    public async Task<IActionResult> AddDevice(string name, string ip, DeviceType type)
    {
        if (ModelState.IsValid)
        {
            _context.Devices.Add(new Device { Name = name, IpAddress = ip, Type = type });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> BackupSelected(List<int> deviceIds)
    {
        // Mass backup logic
        foreach(var id in deviceIds)
        {
             _logger.LogInformation($"Queuing backup for device {id}...");
             await _backupService.PerformBackupAsync(id);
        }
        
        return RedirectToAction(nameof(Index));
    }
}
