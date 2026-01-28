using Microsoft.EntityFrameworkCore;
using HomeRecall;
using HomeRecall.Components;
using MudBlazor.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<HomeRecall.Services.IBackupService, HomeRecall.Services.BackupService>();

builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Setup SQLite
// In HA Addon, config is usually in /config. Locally we use ./data
var persistPath = Environment.GetEnvironmentVariable("persist_path");
if (string.IsNullOrEmpty(persistPath))
{
    persistPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
    Directory.CreateDirectory(persistPath);
}

var dbPath = Path.Combine(persistPath, "homerecall.db");
builder.Services.AddDbContext<BackupContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register background services or other dependencies if needed later
// builder.Services.AddHostedService<BackupScheduler>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Ingress");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Ingress usually handles SSL termination, so HTTP is fine internally, but UseHttpsRedirection ensures correct scheme based on forwarded headers.
// app.UseHttpsRedirection();  // Uncomment if needed, but UseForwardedHeaders sets the scheme 

// Forwarded headers for proxy
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Path Base handling for Ingress
// HA Ingress sets X-Forwarded-Prefix or X-Ingress-Path header. We MUST use this to set PathBase.
app.Use(async (context, next) =>
{
    string? ingressPath = null;
    if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var prefix))
    {
        ingressPath = prefix;
    }
    else if (context.Request.Headers.TryGetValue("X-Ingress-Path", out var path))
    {
        ingressPath = path;
    }
    
    if (!string.IsNullOrEmpty(ingressPath))
    {
        logger.LogInformation($"Setting PathBase to {ingressPath}");
        // Ensure PathBase starts with slash
        var pathBase = new PathString(ingressPath);
        context.Request.PathBase = pathBase;
    }
    await next();
});

// Ensure Static Files are served correctly even behind Ingress paths
app.UseStaticFiles();

app.UseRouting(); // UseRouting must come before Antiforgery and Endpoint Mapping if we manipulate PathBase

app.UseAntiforgery();


app.MapControllers();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Ensure DB created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BackupContext>();
    db.Database.EnsureCreated();
}

app.Run();
