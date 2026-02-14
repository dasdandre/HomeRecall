using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using HomeRecall.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private IBackupService BackupService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private List<Device> _devices = new();
    private HashSet<Device> _selectedItems = new();
    private bool _loading = true;

    // Tracks which devices are currently being backed up to show progress spinners
    private HashSet<int> _isBackingUp = new();
    private bool _isMassBackingUp = false;

    // Filter state
    private string _searchString = "";
    private DeviceType? _filterType;
    private AppSettings? _settings;

    protected override async Task OnInitializedAsync()
    {
        await LoadSettings();
        await LoadDevices();
    }

    private async Task LoadSettings()
    {
        _settings = await Context.Settings.FindAsync(1);
    }

    // QuickFilter used by the MudDataGrid for client-side filtering
    private Func<Device, bool> _quickFilter => x =>
    {
        if (string.IsNullOrWhiteSpace(_searchString))
            return true;

        if (x.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            return true;

        if (x.IpAddress.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            return true;

        if (x.Type.ToString().Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    };

    // Filter logic for the custom Mobile list view
    private bool FilterDevices(Device device)
    {
        // Type Filter
        if (_filterType.HasValue && device.Type != _filterType.Value) return false;

        // Search Filter
        if (string.IsNullOrWhiteSpace(_searchString)) return true;
        if (device.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase)) return true;
        if (device.IpAddress.Contains(_searchString, StringComparison.OrdinalIgnoreCase)) return true;
        if (device.Type.ToString().Contains(_searchString, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private async Task LoadDevices()
    {
        _loading = true;
        // Include Backups to show count and last backup status
        _devices = await Context.Devices.Include(d => d.Backups).ToListAsync();
        _loading = false;
    }

    private void NavigateToBackups(int deviceId)
    {
        NavigationManager.NavigateTo($"backups/{deviceId}");
    }

    private async Task OpenScanDialog()
    {
        var parameters = new DialogParameters();
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ScanDialog>(L["Scan_Title"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            await LoadDevices();
        }
    }

    private async Task OpenAddDeviceDialog()
    {
        var parameters = new DialogParameters();
        var dialog = await DialogService.ShowAsync<AddDeviceDialog>(L["AddDevice_Title"], parameters);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            await LoadDevices();
        }
    }

    private async Task EditDevice(Device device)
    {
        var parameters = new DialogParameters
        {
            { nameof(EditDeviceDialog.DeviceId), device.Id },
            { nameof(EditDeviceDialog.CurrentName), device.Name },
            { nameof(EditDeviceDialog.ExcludeFromAutoBackup), device.AutoBackupOverride == false }
        };

        var dialog = await DialogService.ShowAsync<EditDeviceDialog>(L["EditDevice_Title"], parameters);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
             await LoadDevices();
         }
    }

    // Triggers a backup for a single device and handles UI state
    private async Task BackupDevice(Device device)
    {
        if (_isBackingUp.Contains(device.Id)) return;

        _isBackingUp.Add(device.Id);
        StateHasChanged();

        try
        {
            await BackupService.PerformBackupAsync(device.Id);
        }
        catch(Exception ex)
        {
            Snackbar.Add(String.Format(L["Devices_Backup_Error"], device.Name, ex.Message), Severity.Error);
        }
        finally
        {
            _isBackingUp.Remove(device.Id);
            StateHasChanged();
        }
    }

    private async Task BackupSelected()
    {
        if (!_selectedItems.Any() || _isMassBackingUp) return;

        _isMassBackingUp = true;
        var itemsToBackup = _selectedItems.ToList();

        try
        {
            foreach (var device in itemsToBackup)
            {
                await BackupDevice(device);
            }

            await LoadDevices();
            Snackbar.Add(String.Format(L["Devices_MassBackup_Success"], itemsToBackup.Count), Severity.Success);
        }
        finally
        {
            _isMassBackingUp = false;
            _selectedItems.Clear();
            StateHasChanged();
        }
    }

    private async Task DeleteDevice(Device device)
    {
        // Prevent deletion if locked backups exist
        bool hasLockedBackups = device.Backups.Any(b => b.IsLocked);
        if (hasLockedBackups)
        {
            await DialogService.ShowMessageBox(
                L["Devices_Delete_LockedBackups_Title"],
                String.Format(L["Devices_Delete_LockedBackups_Message"], device.Name),
                L["Common_Understand"]);
            return;
        }

        bool? result = await DialogService.ShowMessageBox(
            L["Devices_Delete_Confirm_Title"],
            String.Format(L["Devices_Delete_Confirm_Message"], device.Name),
            yesText: L["Common_Delete"], cancelText: L["Common_Cancel"]);

        if (result == true)
        {
            foreach (var backup in device.Backups)
            {
                await ExecuteFileCleanup(backup);
            }

            Context.Devices.Remove(device);
            await Context.SaveChangesAsync();
            await LoadDevices();
            Snackbar.Add(L["Devices_Delete_Success"], Severity.Success);
        }
    }

    private async Task DeleteSelected()
    {
        if (!_selectedItems.Any()) return;

        // Check for locked backups across all selected devices
        var devicesWithLockedBackups = _selectedItems.Where(d => d.Backups.Any(b => b.IsLocked)).ToList();
        if (devicesWithLockedBackups.Any())
        {
            var names = string.Join(", ", devicesWithLockedBackups.Select(d => d.Name));
            await DialogService.ShowMessageBox(
                L["Devices_Delete_LockedBackups_Title"],
                String.Format(L["Devices_DeleteSelected_LockedBackups_Message"], names),
                L["Common_Understand"]);
            return;
        }

        bool? result = await DialogService.ShowMessageBox(
            L["Devices_DeleteSelected_Confirm_Title"],
            String.Format(L["Devices_DeleteSelected_Confirm_Message"], _selectedItems.Count),
            yesText: L["Common_Delete"], cancelText: L["Common_Cancel"]);

        if (result == true)
        {
            foreach (var device in _selectedItems)
            {
                foreach (var backup in device.Backups)
                {
                    await ExecuteFileCleanup(backup);
                }
            }

            Context.Devices.RemoveRange(_selectedItems);
            await Context.SaveChangesAsync();
            _selectedItems.Clear();
            await LoadDevices();
            Snackbar.Add(L["Devices_DeleteSelected_Success"], Severity.Success);
        }
    }

    // Helper to physically delete backup files if they are not referenced by other backups
    private async Task ExecuteFileCleanup(Backup backup)
    {
        try
        {
            var otherBackupsUsingFile = await Context.Backups
                .AnyAsync(b => b.StoragePath == backup.StoragePath && b.Id != backup.Id);

            if (!otherBackupsUsingFile)
            {
                var backupDirectory = Environment.GetEnvironmentVariable("backup_path") ?? Path.Combine(Directory.GetCurrentDirectory(), "backups");
                var path = Path.Combine(backupDirectory, backup.StoragePath);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not delete file {backup.StoragePath}: {ex.Message}");
        }
    }
}
