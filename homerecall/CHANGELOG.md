# Changelog

## [1.0.44] - 2026-02-14

### ✨ Added
- **Automated 'Add Device' Workflow**: Devices are now automatically discovered and validated by IP. Metadata like Name, MAC, Firmware, and Model are fetched automatically.
- **Improved Validation**: Real-time IP format and uniqueness checks in the 'Add Device' dialog.
- **Dynamic OpenHasp Backup**: The strategy now lists and downloads all files from the device root dynamically via `/list?dir=/`.

### 🧹 Refactored
- **Code-Behind Migration**: Massive architectural cleanup moving component logic to `.razor.cs` files for better maintainability.
- **OpenHasp Strategy**: Switch to `/api/info/` endpoint and optimized network requests.

### 🌐 Localization
- Complete German and English translations for the new automated discovery and validation features.

## [1.0.43] - 2026-02-13
