# HomeRecall AI Coding Guidelines

## Architecture Overview
HomeRecall is a **Blazor Server web app** for backing up IoT device configurations (Tasmota, WLED, Shelly). Built with .NET 10, it combines SQLite metadata storage with SHA1-based file deduplication and runs as a **Home Assistant add-on** with Ingress proxy support.

**Core Data Flow**: User triggers backup → `BackupService` fetches config from device (HTTP) → Creates ZIP in memory → Computes SHA1 checksum → Checks for duplicates → Stores to disk (deduplicated) → Records metadata in DB (Device/Backup tables).

**Key Design Decisions**:
- **Storage Deduplication**: Multiple backups of the same config share a single `.zip` file on disk (identified by SHA1). DB tracks each backup event separately (CreatedAt, Note) but references the same `StoragePath`.
- **Home Assistant Integration**: Runs behind Ingress reverse proxy; PathBase set via `X-Ingress-Path` header (see `Program.cs` middleware). Data stored in `/config` (DB) and `/backup` (ZIP files) per HA volume mounts.
- **Stateless Services**: `BackupService` is scoped and injectable; no background services (yet). Backups triggered via UI buttons.

## Directory Structure & Key Files
```
homerecall/
  Program.cs              # Blazor Server setup, EF Core, Ingress middleware
  Data.cs                 # BackupContext (DbSet<Device>, DbSet<Backup>)
  Models.cs               # Device, Backup entities; DeviceType enum
  Services/
    BackupService.cs      # Core: HTTP download, ZIP creation, SHA1, deduplication
  Components/
    Pages/
      Home.razor          # Device list (MudTable), backup triggers
      Backups.razor       # Backup history for a device
      AddDeviceDialog.razor  # CRUD dialogs
      RenameDeviceDialog.razor
    Layout/
      MainLayout.razor    # Navigation, HA theme sync
  Controllers/
    DownloadBackupController.cs  # Serve .zip files from disk
```

## Critical Patterns & Code Examples

### Database Operations
Always eager-load related entities to avoid N+1 queries:
```csharp
// Good: Include Backups when fetching Device
var device = await _context.Devices.Include(d => d.Backups).FirstAsync();

// In Home.razor: Load all devices with backups
var _devices = await Context.Devices.Include(d => d.Backups).ToListAsync();
```
Cascade delete is configured: removing a Device deletes all its Backups.

### Device-Specific API Endpoints
In `BackupService.PerformBackupAsync()`, switch on `DeviceType` enum:
- **Tasmota**: `http://{ip}/dl` → downloads `Config.dmp`
- **WLED**: `http://{ip}/edit?download=cfg.json` → downloads `cfg.json`
- **Shelly**: `http://{ip}/settings` → downloads JSON (Gen1; Gen2 needs RPC calls)

Each device type needs unique implementation; HTTP timeout is 10 seconds.

### SHA1 Deduplication Logic
```csharp
// Check if last backup has same checksum (device-level dedup)
var lastBackup = await _context.Backups
    .Where(b => b.DeviceId == device.Id)
    .OrderByDescending(b => b.CreatedAt)
    .FirstOrDefaultAsync();
bool isDuplicate = lastBackup?.Sha1Checksum == checksum;

// Check if file exists on disk (global dedup)
string storageFileName = $"{checksum}.zip";
if (!File.Exists(storagePath)) {
    await File.WriteAllBytesAsync(storagePath, zipBytes);
}

// Always create DB record (even for duplicates) to track history
var backup = new Backup { DeviceId, CreatedAt, Sha1Checksum, StoragePath };
_context.Backups.Add(backup);
```
**Critical**: DB entry is created **regardless** of deduplication (for history tracking), but ZIP file is only written if it doesn't already exist on disk.

### MudBlazor UI Components
- **MudTable**: Device list in `Home.razor` with clickable rows, pagination, sorting
- **MudDialog**: Device CRUD via `AddDeviceDialog.razor`, `RenameDeviceDialog.razor`
- **MudButton**: Backup, view history, delete actions (use `Variant.Outlined`, `Color.Primary`)
- **MudSnackbar**: User notifications (inject `ISnackbar`); no modal alerts—use `Snackbar.Add(message, Severity.Success)`

### Home Assistant Integration
**Environment Variables** (read in `Program.cs` and `BackupService` constructor):
- `persist_path` → SQLite DB location (default: `./data`)
- `backup_path` → ZIP storage location (default: `./backups`)

**Ingress Middleware** (Program.cs):
```csharp
app.Use(async (context, next) => {
    if (context.Request.Headers.TryGetValue("X-Ingress-Path", out var ingressPath)) {
        context.Request.PathBase = new PathString(ingressPath);
    }
    await next();
});
```
Always verify routing works behind a path prefix (e.g., `/addons/homerecall/`).

### Error Handling & Logging
- **Services**: Use `ILogger<T>` for structured logging (already injected in `BackupService`)
- **UI**: Use `ISnackbar.Add(message, Severity.Error)` for user-facing errors
- **HTTP Errors**: `DownloadFileAsync()` throws `HttpRequestException`; catch in `PerformBackupAsync()` and log, don't propagate
- **File I/O**: Ensure `backup_path` directory exists; handle disk full gracefully

## Development Workflows

### Local Development
```bash
cd homerecall
dotnet watch run
```
- Launches on `http://localhost:5000` with Blazor hot reload
- Uses `./data` and `./backups` local directories
- `launchSettings.json` can mock Home Assistant paths if needed

### Build & Publish
```bash
# Release build for Docker
dotnet publish -c Release -o ./publish

# Or direct Docker build (multi-stage: SDK → Alpine runtime)
docker build -t homerecall .
```

### Docker Container (Local Testing)
```bash
docker run -d \
  -p 5000:8080 \
  -v $(pwd)/data:/config \
  -v $(pwd)/backups:/backup \
  homerecall
```
Container uses Alpine Linux + .NET 10 runtime. Entry point is `dotnet homerecall.dll` on port 8080 (mapped to 5000).

### Home Assistant Add-on Deployment
1. Update version in `config.yaml`
2. Push to GitHub repo
3. HA Addon system auto-detects via `build.yaml` and builds multi-arch (aarch64, amd64, armhf, armv7)
4. Addon installs via HA UI; Ingress automatically proxies to port 8080 behind `/addons/homerecall/`

## Conventions & Style
- **Namespace**: `HomeRecall` (matches assembly)
- **Async/Await**: Used throughout (HTTP, DB, file I/O)
- **UI Language**: German (keep consistent: "Geräte", "Neues Gerät", "Backup", etc.)
- **Null Safety**: Enabled (`<Nullable>enable</Nullable>`); use `!` suppression only when certain
- **Entity Relationships**: One-to-many (Device → Backups); configure in `BackupContext.OnModelCreating()`
- **File Naming**: Device config ZIPs named by SHA1 checksum: `abc123def456....zip`

## Common Tasks
- **Add device type**: Extend `DeviceType` enum, add case in `BackupService.PerformBackupAsync()`
- **Add UI page**: Create `.razor` file in `Components/Pages/`, add `@page` route, inject `BackupContext` and services
- **Add database field**: Modify model class, run `dotnet ef migrations add <name>`, apply with `db.Database.Migrate()`
- **Test backup logic**: Use mock device IP in local network; check `./backups/` for `.zip` files and `./data/homerecall.db` for Backup records</content>
<parameter name="filePath">c:\Users\fabri\Documents\HomeRecall\.github\copilot-instructions.md