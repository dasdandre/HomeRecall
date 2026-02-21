using HomeRecall.Persistence;
using HomeRecall.Services;
using HomeRecall.Services.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        // Add default HttpClient for making network requests to devices
        services.AddHttpClient();

        // Add specialized HttpClient for Device Scanner to allow log filtering
        services.AddHttpClient("DeviceScanner");

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
        services.AddSingleton<IDeviceStrategy, TasmotaStrategy>();
        services.AddSingleton<IDeviceStrategy, WledStrategy>();
        services.AddSingleton<IDeviceStrategy, ShellyStrategy>();
        services.AddSingleton<IDeviceStrategy, ShellyGen2Strategy>();
        services.AddSingleton<IDeviceStrategy, OpenDtuStrategy>();
        services.AddSingleton<IDeviceStrategy, AiOnTheEdgeStrategy>();
        services.AddSingleton<IDeviceStrategy, AwtrixStrategy>();
        services.AddSingleton<IDeviceStrategy, OpenHaspStrategy>();
    }

    /// <summary>
    /// Configures logging based on environment options.json.
    /// Default is info.
    /// </summary>
    public static void ConfigureLogging(this ILoggingBuilder logging)
    {
        var optionsPath = Environment.GetEnvironmentVariable("options_path") ?? "/data/options.json";
        string? haLogLevel = "info";

        if (File.Exists(optionsPath))
        {
            try
            {
                var json = File.ReadAllText(optionsPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("log_level", out var logLevelElement))
                {
                    haLogLevel = logLevelElement.GetString()?.ToLowerInvariant();
                }
            }
            catch
            {
                // Fallback or ignore if unreadable
            }
        }

        LogLevel minLevel = LogLevel.Information;
        switch (haLogLevel)
        {
            case "trace": minLevel = LogLevel.Trace; break;
            case "debug": minLevel = LogLevel.Debug; break;
            case "warning": minLevel = LogLevel.Warning; break;
            case "error": minLevel = LogLevel.Error; break;
            case "critical": minLevel = LogLevel.Critical; break;
            case "none": minLevel = LogLevel.None; break;
            default: minLevel = LogLevel.Information; break;
        }

        logging.SetMinimumLevel(minLevel);

        // Configure EF Core logging based on Addon log_level
        logging.AddFilter((category, level) =>
        {
            if (category == "Microsoft.EntityFrameworkCore.Database.Command" && level == LogLevel.Information)
            {
                // Only show DB command logs if HA log_level is debug or trace
                return minLevel <= LogLevel.Debug;
            }

            if (category != null && category.StartsWith("System.Net.Http.HttpClient.DeviceScanner"))
            {
                // Suppress the flood of endpoint unreachable errors during IP Scans
                // normally logged by the HttpClientFactory's logging handler.
                return minLevel <= LogLevel.Trace;
            }

            return level >= minLevel;
        });
    }
}
