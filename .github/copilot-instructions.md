# HomeRecall AI Coding Guidelines

## Architecture Overview
HomeRecall is a **Blazor Server web app** for backing up IoT device configurations (Tasmota, WLED, Shelly Gen1/Gen2, OpenDTU, etc.). Built with **.NET 10**, it combines SQLite metadata storage with SHA1-based file deduplication and runs as a **Home Assistant add-on** with Ingress proxy support.

**Core Data Flow**: User triggers backup → `BackupService` delegates to specific `IDeviceStrategy` → Strategy fetches config (HTTP) → Service creates ZIP in memory (deterministic timestamp) → Computes SHA1 checksum of raw content → Checks for duplicates (content-based) → Stores to disk (if new) → Records metadata in DB.

**Key Design Decisions**:
- **Strategy Pattern**: Each device type (Tasmota, WLED, Shelly, etc.) has its own `IDeviceStrategy` implementation in `Services/Strategies/`.
- **Content-Based Deduplication**: Deduplication is based on the hash of the *configuration content*, not the ZIP file. If the content is identical to the previous backup, the *existing file path* is reused in the database, and no new file is written.
- **Home Assistant Integration**: Runs behind Ingress reverse proxy; PathBase set via `X-Ingress-Path` header. Themes are synced from HA via JS Interop.
- **Hosted Services**: `BackupScheduler` runs in the background for automated backups based on retention policies.

## Directory Structure & Key Files
```
homerecall/
  Program.cs              # Entry point, Service DI, Middleware (Ingress), EF Migration
  Data.cs                 # BackupContext
  Models.cs               # Device, Backup, AppSettings entities
  Services/
    BackupService.cs      # Core orchestration: ZIP creation, Deduplication, Storage
    IDeviceStrategy.cs    # Interface for device strategies
    Strategies/           # Implementations: TasmotaStrategy.cs, WledStrategy.cs, ShellyGen2Strategy.cs etc.
    BackupScheduler.cs    # Background service for auto-backups and retention
    DeviceScanner.cs      # Network scanner for discovery
  Components/
    Pages/
      Home.razor          # Dashboard, Device list
      Backups.razor       # Backup history (Breadcrumbs used here)
      Settings.razor      # Global settings (Retention, Auto-Backup)
      ScanDialog.razor    # Network scanner UI
    Layout/
      MainLayout.razor    # Theme logic, Navigation
  Controllers/
    DownloadBackupController.cs  # File download endpoint
```

## Critical Patterns & Code Examples

### Backup & Deduplication Logic (`BackupService.cs`)
```csharp
// 1. Fetch data via Strategy
var result = await strategy.BackupAsync(device, httpClient);

// 2. Deterministic ZIP Creation
// - Sort files alphabetically
// - Use fixed timestamp (2000-01-01) for ZipArchiveEntries

// 3. Content Hash Calculation
// - Hash raw file contents (not the ZIP container)
string checksum = CalculateContentHash(sortedFiles);

// 4. Check Previous Backup
var lastBackup = await _context.Backups...FirstOrDefaultAsync();
if (lastBackup != null && lastBackup.Sha1Checksum == checksum) {
    // REUSE existing file path
    storageFileName = lastBackup.StoragePath;
} else {
    // CREATE new file
    storageFileName = $"{Date}_{Name}_{Type}_{Hash}.zip";
    await File.WriteAllBytesAsync(path, zipBytes);
}

// 5. Always Create DB Entry
_context.Backups.Add(new Backup { ... StoragePath = storageFileName ... });
```

### Device Strategies (`IDeviceStrategy`)
To add a new device:
1. Create `NewDeviceStrategy.cs` implementing `IDeviceStrategy`.
2. Register in `Program.cs`: `builder.Services.AddScoped<IDeviceStrategy, NewDeviceStrategy>();`.
3. Add enum value to `DeviceType` in `Models.cs`.

**Key Strategy Details**:
- **Multi-File Support**: Strategies can return multiple files (e.g., WLED returns `cfg.json` + `presets.json`).
- **Shelly Distinction**: Separate strategies for `Shelly` (Gen1 via `/settings`) and `ShellyGen2` (Plus/Pro via RPC).
- **Firmware Version**: Strategies should attempt to extract firmware version during backup.

### Network Scanning (`DeviceScanner.cs`)
- **Parallelism**: Uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 20`.
- **Logic**: Scans IP range, trying all selected strategies against each IP.
- **Persistence**: Last scan settings (Start IP, Range, Types) are saved in `AppSettings`.

### Ingress & Navigation
- **PathBase**: Handled in `Program.cs` via `X-Ingress-Path`.
- **Links**: Use `NavigationManager`.
- **Absolute URIs**: For components requiring absolute URLs (like `MudBreadcrumbs`), use `NavigationManager.ToAbsoluteUri(...)` to correctly respect the Ingress base path.

```csharp
// Correct Breadcrumb Usage
new BreadcrumbItem(L["Nav_Devices"], href: NavigationManager.ToAbsoluteUri("").ToString()),
```

### Database & Migrations
- **SQLite**: Data stored in `persist_path` (default `./data` or `/data` in Docker).
- **Backups**: ZIPs stored in `backup_path` (default `./backups` or `/data/backups` in Docker).
- **HA Backups**: By storing in `/data`, backups are automatically included in HA snapshots.
- **Auto-Migrate**: `db.Database.Migrate()` is called in `Program.cs` on startup.

## Development Workflows
- **Run Local**: `dotnet watch run` (uses `./data` and `./backups`).
- **Docker**: Dockerfile provided for multi-arch builds.
- **Home Assistant**: Deploy as Add-on. `config.yaml` and `build.yaml` configure the addon.

## Conventions
- **Language**: UI supports EN/DE via `IStringLocalizer`.
- **UI Lib**: MudBlazor.
- **Async**: All I/O operations must be async.
- **Logging**: Use injected `ILogger`.
