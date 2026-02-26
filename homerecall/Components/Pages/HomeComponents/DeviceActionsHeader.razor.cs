using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace HomeRecall.Components.Pages.HomeComponents;

public partial class DeviceActionsHeader : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;

    [Parameter] public bool HasSelectedItems { get; set; }
    [Parameter] public int DevicesCount { get; set; }
    [Parameter] public int SelectedItemsCount { get; set; }
    [Parameter] public bool IsMassBackingUp { get; set; }

    [Parameter] public EventCallback OnBackupSelected { get; set; }
    [Parameter] public EventCallback OnDeleteSelected { get; set; }
    [Parameter] public EventCallback OnOpenScanDialog { get; set; }
    [Parameter] public EventCallback OnOpenAddDeviceDialog { get; set; }
}
