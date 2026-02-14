using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class AddDeviceDialog : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private Device model = new Device();
    private bool success;

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        Context.Devices.Add(model);
        await Context.SaveChangesAsync();
        Snackbar.Add(L["AddDevice_Success"], Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }
}
