ip# HomeRecall Context

## Project Overview
HomeRecall is a **.NET 10** Blazor Server application designed to backup configurations and data from various IoT devices commonly used in smart homes. It is specifically optimized to run as a Home Assistant Add-on but can also run standalone via Docker.

## Tech Stack
- **Framework:** .NET 10 (ASP.NET Core Blazor Server)
- **UI Library:** MudBlazor
- **Database:** SQLite (using Entity Framework Core)
- **Containerization:** Docker

## Core Architecture

### 1. Device Strategies (`homerecall/Services/Strategies/`)
The application uses the Strategy pattern (`IDeviceStrategy`) to support multiple device types.
- **Supported Devices:** Tasmota, WLED, Shelly (Gen1 & Gen2), OpenDTU, AI-on-the-Edge, Awtrix, OpenHASP.
- **Capabilities:** Strategies are responsible for:
    - **Backup:** Fetching config files (supports multi-file backups).
    - **Probing:** Discovering devices, performing "Smart Naming" (e.g. resolving Tasmota FriendlyNames), and extracting **Firmware Versions**.
- **Scanner:** `DeviceScanner` scans the local network (HTTP/Parallel) to discover compatible devices.
    - **Persistence:** Last scan range and selected device types are saved in `AppSettings`.

### 2. Backup Strategy
The `BackupService` implements a robust, deduplicating backup process:

1.  **Extraction:**
    - The appropriate `IDeviceStrategy` fetches configuration files via HTTP (30s timeout).
    - Returns a list of `BackupFile` objects (filename + byte content) and the detected Firmware Version.

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
    - **Database:** Stores metadata (DeviceID, Timestamp, Checksum, StoragePath, LockedStatus, Note, FirmwareVersion).

5.  **Retention Policy:**
    - Controlled by `BackupScheduler` (Hosted Service).
    - Policies: Smart (thinning), Simple Days, Simple Count, or Keep All.
    - **Locked Backups:** Backups marked as "Locked" are never automatically deleted.

### 3. Home Assistant Integration (Ingress)
This is a critical aspect of the application configuration.
- **Path Base:** Home Assistant Ingress serves the app under a dynamic sub-path.
  - **Middleware:** `Program.cs` reads the `X-Ingress-Path` header to set `context.Request.PathBase`.
  - **Blazor Base:** `App.razor` dynamically calculates the `<base href="..." />` tag using the `X-Ingress-Path` header or falls back to `NavigationManager.BaseUri`.
  - **Navigation:** Links must be relative or correctly resolved. `NavigationManager.NavigateTo` generally handles relative paths well, but explicit URI construction (like for Breadcrumbs) must use `NavigationManager.ToAbsoluteUri()`.
- **Theming:**
  - `MainLayout.razor` uses JS Interop (`wwwroot/js/ha-theme.js`) to read Home Assistant's current colors and dark/light mode, applying them to the MudBlazor theme dynamically.

## Key Files & Directories
- `homerecall/`
  - `Program.cs`: App entry point, Service registration, Middleware config (Ingress).
  - `Components/`
    - `App.razor`: Root component, handles `<base href>` logic.
    - `Layout/MainLayout.razor`: Global layout, Theme sync logic.
    - `Pages/`:
      - `Home.razor`: Dashboard, Device list.
      - `Backups.razor`: Backup history for a specific device.
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

## Recent Focus
- **Device Strategies:** Improved Tasmota strategy to probe for Friendly Name, Device Name, or Hostname instead of generic naming.
- **Breadcrumb Navigation:** Fixed absolute path generation for Breadcrumbs to work correctly with Home Assistant Ingress using `NavigationManager.ToAbsoluteUri`.
