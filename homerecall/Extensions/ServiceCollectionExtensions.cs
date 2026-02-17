using HomeRecall.Persistence;
using HomeRecall.Services;
using HomeRecall.Services.Strategies;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

namespace HomeRecall.Extensions;

public static class ServiceCollectionExtensions
{   
    /// <summary>
    /// Configures the persistence layer, including SQLite database connection and EF Core context.
    /// Handles database path resolution for Docker/Add-on environments.
    /// </summary>
    public static void AddPersistence(this IServiceCollection services)
    {
        // In HA Addon, config is usually in /config. Locally we use ./data
        var persistPath = Environment.GetEnvironmentVariable("persist_path");
        if (string.IsNullOrEmpty(persistPath))
        {
            persistPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
        }

        // Ensure the directory exists (important if persist_path is provided via env var)
        if (!Directory.Exists(persistPath))
        {
            Directory.CreateDirectory(persistPath);
        }

        // Use absolute path for SQLite to avoid issues with relative paths in connection strings
        var dbFullFilename = Path.GetFullPath(Path.Combine(persistPath, "homerecall.db"));
        services.AddDbContext<BackupContext>(options =>
        {
            options.UseSqlite($"Data Source={dbFullFilename}");
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.DetachedLazyLoadingWarning));
        });
    }

    /// <summary>
    /// Registers core application services, including HTTP clients, localization, background workers, and UI services.
    /// </summary>
    public static void AddApplicationServices(this IServiceCollection services)
    {
        // Add Controllers for API endpoints (e.g. DownloadBackupController)
        services.AddControllers();

        // Add HttpClient for making network requests to devices
        services.AddHttpClient();

        // Add Localization support (ResourcesPath = "Resources")
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Domain Services
        services.AddScoped<IBackupService, BackupService>();

        // Register all strategies for different device types (Tasmota, Shelly, etc.)
        services.AddDeviceStrategies();
        services.AddScoped<IDeviceScanner, DeviceScanner>();

        // Background Service for scheduled backups
        services.AddHostedService<BackupScheduler>();

        // UI Services (MudBlazor)
        services.AddMudServices();
        services.AddHttpContextAccessor(); // Required for some UI components or accessing HttpContext

        // Data Protection for secure storage
        services.AddDataProtection();

        // MQTT Service (Singleton to maintain connection)
        services.AddSingleton<IMqttService, MqttService>();

        // Add Razor Components with Interactive Server Mode support
        services.AddRazorComponents().AddInteractiveServerComponents();
    }


    /// <summary>
    /// Registers all available device strategies for discovery and backup.
    /// </summary>
    public static void AddDeviceStrategies(this IServiceCollection services)
    {
        services.AddScoped<IDeviceStrategy, TasmotaStrategy>();
        services.AddScoped<IDeviceStrategy, WledStrategy>();
        services.AddScoped<IDeviceStrategy, ShellyStrategy>();
        services.AddScoped<IDeviceStrategy, ShellyGen2Strategy>();
        services.AddScoped<IDeviceStrategy, OpenDtuStrategy>();
        services.AddScoped<IDeviceStrategy, AiOnTheEdgeStrategy>();
        services.AddScoped<IDeviceStrategy, AwtrixStrategy>();
        services.AddScoped<IDeviceStrategy, OpenHaspStrategy>();
    }
}
