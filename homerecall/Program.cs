using Microsoft.EntityFrameworkCore;
using HomeRecall;
using HomeRecall.Components;
using MudBlazor.Services;
using HomeRecall.Services;
using HomeRecall.Persistence;
using HomeRecall.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddScoped<IBackupService, BackupService>();
// Register all strategies
builder.Services.AddDeviceStrategies();
builder.Services.AddScoped<IDeviceScanner, DeviceScanner>();

builder.Services.AddHostedService<HomeRecall.Services.BackupScheduler>();

builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Setup SQLite
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
var dbPath = Path.GetFullPath(Path.Combine(persistPath, "homerecall.db"));
builder.Services.AddDbContext<BackupContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.DetachedLazyLoadingWarning));
});

// Reduce EF Core logging noise by default (only Warning and Error), but respect appsettings config
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


// Path Base handling for Ingress
// HA Ingress sets HTTP_X_INGRESS_PATH header. We MUST use this to set PathBase.
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Ingress-Path", out var ingressPath))
    {
        // Ensure PathBase starts with slash
        var pathBase = new PathString(ingressPath);
        context.Request.PathBase = pathBase;
    }
    await next();
});

// Ensure Static Files are served correctly even behind Ingress paths
app.UseStaticFiles();

var supportedCultures = new[] { "en", "de" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.UseAntiforgery();
app.MapControllers();

// Ingress Setup:
// Home Assistant Ingress sends requests to the root, but we need to handle the base path correctly if we were using it directly.
// However, Ingress usually proxies it nicely. We might need X-Forwarded-For headers etc.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Ensure DB created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BackupContext>();
    db.Database.Migrate();
    // Enable Write-Ahead Logging (WAL) for better performance and resilience against power loss
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

app.Run();

