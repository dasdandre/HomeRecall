using Microsoft.EntityFrameworkCore;
using HomeRecall;
using HomeRecall.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<HomeRecall.Services.IBackupService, HomeRecall.Services.BackupService>();

builder.Services.AddMudServices();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Ingress usually handles SSL termination, so HTTP is fine internally, but standard practice:
// app.UseHttpsRedirection(); 

// Path Base handling for Ingress MUST be before Static Files
// HA Ingress sets HTTP_X_INGRESS_PATH header. If present, we should use it as PathBase.
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Ingress-Path", out var ingressPath))
    {
        context.Request.PathBase = new PathString(ingressPath);
    }
    await next();
});

// Ensure Static Files are served correctly even behind Ingress paths
app.UseStaticFiles();

app.UseRouting(); // UseRouting must come before Antiforgery and Endpoint Mapping if we manipulate PathBase

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
    db.Database.EnsureCreated();
}

app.Run();
