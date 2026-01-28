# HomeRecall AI Coding Guidelines

## Architecture Overview
HomeRecall is a Blazor Server web app for backing up IoT device configurations (Tasmota, WLED, Shelly). It uses SQLite for metadata, SHA1-based deduplication for storage, and runs as a Home Assistant add-on with Ingress reverse proxy support.

- **Core Components**: `Program.cs` sets up Blazor Server, EF Core SQLite, and Ingress path handling. `BackupService.cs` handles device-specific HTTP downloads and ZIP creation.
- **Data Flow**: Devices → HTTP fetch config → ZIP with SHA1 → Deduplicated storage → DB tracking.
- **Key Patterns**: Environment variables (`persist_path`, `backup_path`) for HA addon compatibility; German UI text; MudBlazor components.

## Development Workflows
- **Local Run**: `dotnet watch run` (uses `launchSettings.json` for dev paths).
- **Build**: `dotnet publish -c Release` for production.
- **Container**: `docker build -t homerecall .` then `docker run -v ./data:/config -v ./backups:/backup -p 5000:8080 homerecall`.
- **HA Addon**: Update `config.yaml` version, build via HA addon system using `build.yaml` and `Dockerfile`.

## Code Patterns
- **Device Backups**: In `BackupService.cs`, use device type enums for API endpoints (e.g., Tasmota: `http://{ip}/dl`).
- **DB Operations**: Always include related entities (e.g., `Context.Devices.Include(d => d.Backups)` in `Home.razor`).
- **UI Components**: MudBlazor tables for lists (`MudTable` in `Home.razor`), dialogs for CRUD (`AddDeviceDialog.razor`).
- **File Handling**: Store backups in `backup_path` directory, deduplicate by SHA1 (check existing files before writing).
- **Error Handling**: Use `ISnackbar` for user feedback, log errors in services.

## Key Files
- [Program.cs](homerecall/Program.cs): App setup, Ingress middleware.
- [BackupService.cs](homerecall/Services/BackupService.cs): Backup logic with deduplication.
- [Home.razor](homerecall/Components/Pages/Home.razor): Device list and actions.
- [Models.cs](homerecall/Models.cs): EF entities (Device, Backup).
- [config.yaml](homerecall/config.yaml): HA addon configuration.

## Conventions
- Namespace: `HomeRecall` (matches assembly).
- Async/Await: Use throughout for I/O (HTTP, DB, file ops).
- Localization: UI in German; keep consistent.
- Testing: No explicit tests yet; validate with manual runs and `dotnet watch` hot reload.</content>
<parameter name="filePath">c:\Users\fabri\Documents\HomeRecall\.github\copilot-instructions.md