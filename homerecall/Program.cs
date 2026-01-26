using Microsoft.EntityFrameworkCore;
using HomeRecall;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<HomeRecall.Services.IBackupService, HomeRecall.Services.BackupService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();  

// Setup SQLite
// In HA Addon, config is usually in /config
var dbPath = Path.Combine(Environment.GetEnvironmentVariable("persist_path") ?? "/config", "homerecall.db");
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
app.UseStaticFiles();

app.UseRouting();

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
