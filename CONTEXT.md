# HomeRecall Context

## Project Overview
HomeRecall is a **.NET 10** Blazor Server application designed to backup configurations and data from various IoT devices commonly used in smart homes. It is specifically optimized to run as a Home Assistant Add-on but can also run standalone via Docker.

## Tech Stack
- **Framework:** .NET 10 (ASP.NET Core Blazor Server)
- **UI Library:** MudBlazor (Standard Material Design with HA Colors)
- **Database:** SQLite (using Entity Framework Core)
- **Containerization:** Docker

## Core Architecture

### 1. Device Strategies (`homerecall/Services/Strategies/`)
The application uses the Strategy pattern (`IDeviceStrategy`) to support multiple device types.
- **Supported Devices:** Tasmota, WLED, Shelly (Gen1 & Gen2), OpenDTU, AI-on-the-Edge, Awtrix, OpenHASP.
- **Capabilities:** Strategies are responsible for:
    - **Backup:** Fetching config files (supports multi-file backups).
    - **Probing:** Discovering devices, performing "Smart Naming" (e.g. resolving Tasmota FriendlyNames), extracting **Hardware Model** and **Firmware Versions**.
- **Scanner:** `DeviceScanner` scans the local network (HTTP/Parallel) to discover compatible devices.
    - **Persistence:** Last scan range and selected device types are saved in `AppSettings`.

### 2. Backup Strategy
The `BackupService` implements a robust, deduplicating backup process:

1.  **Extraction:**
    - The appropriate `IDeviceStrategy` fetches configuration files via HTTP (30s timeout).
    - Returns a list of `BackupFile` objects (filename + byte content) and the detected Firmware Version.
    - **Awtrix** backups are recursive (download all files via `/list?dir=...`).

2.  **Determinism:**
    - Files are sorted alphabetically by name.
    - ZIP entries use a fixed timestamp (`2000-01-01`) to ensure binary reproducibility.

3.  **Deduplication (Content-Addressable-ish):**
    - A **SHA1 Checksum** is calculated based on the *raw concatenated content* of the config files (not the ZIP container).
    - This hash is compared against the *immediately preceding* backup for the same device.
    - **Match:** If the hash matches, the **existing file path** is reused. No new file is written to disk (unless the physical file is missing). A new DB entry is created pointing to the old file.
    - **No Match:** If content differs, a new ZIP file is created and saved.

4.  **Storage:**
    - **Naming Convention:** `YYYY-MM-DD_HH-mm-ss_{DeviceName}_{DeviceType}_{ShortHash}.zip`
    - **Location:** determined by `backup_path` environment variable.
        - **Local Dev:** `./backups`
        - **Docker/HA:** `/data/backups` (ensures inclusion in HA snapshots)
    - **Database:** Stores metadata (DeviceID, Timestamp, Checksum, StoragePath, LockedStatus, Note, FirmwareVersion, **BackupSize**).

5.  **Retention Policy:**
    - Controlled by `BackupScheduler` (Hosted Service).
    - Policies: Smart (thinning), Simple Days, Simple Count, or Keep All.
    - **Locked Backups:** Backups marked as "Locked" are never automatically deleted.

### 3. Home Assistant Integration (Ingress)
This is a critical aspect of the application configuration.
- **Path Base:** Home Assistant Ingress serves the app under a dynamic sub-path.
  - **Middleware:** `Program.cs` reads the `X-Ingress-Path` header to set `context.Request.PathBase`.
  - **Blazor Base:** `App.razor` dynamically calculates the `<base href="..." />` tag using the `X-Ingress-Path` header or falls back to `NavigationManager.BaseUri`.
  - **Theming:**
    - `MainLayout.razor` applies static "Home Assistant Blue" colors to match the default HA theme.
    - Automatic System Dark Mode detection is enabled via `MudThemeProvider`.

## Key Files & Directories
- `homerecall/`
  - `Program.cs`: App entry point, Service registration, Middleware config (Ingress).
  - `Components/`
    - `App.razor`: Root component, handles `<base href>` logic.
    - `Layout/MainLayout.razor`: Global layout, Theme logic.
    - `Pages/`:
      - `Home.razor`: Dashboard Controller.
        - `HomeComponents/`: `DeviceActionsHeader`, `DeviceMobileList` (Cards), `DeviceDataGrid` (Table).
      - `Backups.razor`: Backup history Controller.
        - `BackupComponents/`: `BackupMobileList`, `BackupTable`.
      - `Settings.razor`: Global settings.
  - `Services/`:
    - `BackupService.cs`: Core logic described above.
    - `Strategies/`: Device specific implementations.
  - `Data.cs` / `Models.cs`: EF Core DbContext and Entities (`Device`, `Backup`, `AppSettings`).
  - `Controllers/`: API endpoints (e.g., for file downloads).

## Configuration
- **Environment Variables:**
  - `persist_path`: Directory for SQLite DB.
    - Local: `./data`
    - Docker: `/data`
  - `backup_path`: Directory for backup files.
    - Local: `./backups`
    - Docker: `/data/backups`
- **Data Storage:**
  - **Home Assistant:** Uses `/data` volume for persistence. Backups stored in `/data/backups` are automatically included in Home Assistant's full snapshots.

## Recent Changes
- **Refactoring:** Split monolithic `Home.razor` and `Backups.razor` into smaller, maintainable sub-components (`HomeComponents/`, `BackupComponents/`) separating Mobile (Cards) and Desktop (DataGrid) views.
- **Theming:** Removed complex JS-based dynamic theme syncing with Home Assistant. Now uses robust static colors and native System Dark Mode detection.
- **Code Clean:** Removed obsolete method calls in `MudThemeProvider`.
- **UI:** Improved Desktop DataGrid layout with compact `MudButtonGroup` and colored `MudChips` for device types. Added Backup Size column.
- **Features:** 
    - Added `HardwareModel` to devices.
    - Added `BackupSize` to backups.
    - Improved Shelly Gen2/3 backup (includes scripts).
    - Improved Awtrix backup (recursive file download).
    - Reduced EF Core logging noise.
- **Scanner:** 
    - **Live View:** Found devices are now displayed in real-time during the scan.
    - **Control:** Added "Stop Scan" button and proper cancellation support using `CancellationToken`.
    - **Feedback:** Displays number of skipped (known) IPs.
- **Strategies:**
    - **Tasmota:** Added logic to query `cm?cmnd=Module` to retrieve the specific Hardware Model (e.g. "Sonoff Basic", "Refoss-P11").