using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class Backups : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    [Parameter]
    public int DeviceId { get; set; }

    private Device? _device;
    private bool _loading = true;
    private HashSet<Backup> _selectedItems = new();
    private List<BreadcrumbItem> _breadcrumbs = new List<BreadcrumbItem>();

    protected override async Task OnInitializedAsync()
    {
        await LoadData();

        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem(L["Nav_Devices"], href: NavigationManager.ToAbsoluteUri("").ToString()),
            new BreadcrumbItem(L["Common_Backups"], href: null, disabled: true)
        };
    }

    private async Task LoadData()
    {
        _loading = true;
        _device = await Context.Devices
            .Include(d => d.Backups)
            .FirstOrDefaultAsync(d => d.Id == DeviceId);
        _loading = false;
    }

    private async Task ToggleLock(Backup backup)
    {
        backup.IsLocked = !backup.IsLocked;
        await Context.SaveChangesAsync();
    }

    // Handles note updates from both Mobile and Desktop views
    private async Task UpdateNote((Backup backup, string note) args)
    {
        args.backup.Note = args.note;
        await Context.SaveChangesAsync();
    }

    private async Task DeleteBackup(Backup backup)
    {
        if (backup.IsLocked) return;

        bool? result = await DialogService.ShowMessageBox(
            L["Backups_Delete_Confirm_Title"],
            L["Backups_Delete_Confirm_Message"],
            yesText:L["Common_Delete"], cancelText:L["Common_Cancel"]);

        if (result == true)
        {
            await ExecuteDelete(backup);
            await LoadData();
            Snackbar.Add(L["Backups_Deleted_Success"], Severity.Success);
        }
    }

    private async Task DeleteSelectedBackups()
    {
        var deletableItems = _selectedItems.Where(b => !b.IsLocked).ToList();
        if (!deletableItems.Any())
        {
            Snackbar.Add(L["Backups_DeleteSelected_Warning"], Severity.Warning);
            return;
        }

        bool? result = await DialogService.ShowMessageBox(
            L["Backups_DeleteSelected_Confirm_Title"],
            String.Format(L["Backups_DeleteSelected_Confirm_Message"], deletableItems.Count),
            yesText:L["Common_Delete"], cancelText:L["Common_Cancel"]);

        if (result == true)
        {
            foreach (var backup in deletableItems)
            {
                await ExecuteDelete(backup);
            }
            _selectedItems.Clear();
            await LoadData();
            Snackbar.Add(String.Format(L["Backups_DeletedMultiple_Success"], deletableItems.Count), Severity.Success);
        }
    }

    // Physically removes file and database entry
    private async Task ExecuteDelete(Backup backup)
    {
        // Check if file is used by others (deduplication check)
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

        Context.Backups.Remove(backup);
        await Context.SaveChangesAsync();
    }

    private void DownloadBackup(Backup backup)
    {
        NavigationManager.NavigateTo($"api/DownloadBackup/{backup.Id}", true);
    }
}
