using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class EditDeviceDialog : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public int DeviceId { get; set; }
    [Parameter] public string CurrentName { get; set; } = string.Empty;
    [Parameter] public bool ExcludeFromAutoBackup { get; set; } = false;

    public string NewName { get; set; } = string.Empty;

    private bool success;

    protected override void OnInitialized()
    {
        NewName = CurrentName;
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        var device = await Context.Devices.FindAsync(DeviceId);
        if (device != null)
        {
            device.Name = NewName;

            // If Exclude is true, we set Override to false (meaning: explicit NO).
            // If Exclude is false, we set Override to null (default/inherit).
            device.AutoBackupOverride = ExcludeFromAutoBackup ? false : null;

            await Context.SaveChangesAsync();
            Snackbar.Add(L["EditDevice_Success"], Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add(L["RenameDevice_NotFound"], Severity.Error);
            MudDialog.Cancel();
        }
    }
}
