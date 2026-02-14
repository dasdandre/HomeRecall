using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace HomeRecall.Components.Pages.HomeComponents;

public partial class DeviceMobileList : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;

    [Parameter] public List<Device> Devices { get; set; } = new();
    [Parameter] public HashSet<int> IsBackingUp { get; set; } = new();

    // Filtering Parameters
    [Parameter] public string SearchString { get; set; } = "";
    [Parameter] public EventCallback<string> SearchStringChanged { get; set; }

    [Parameter] public DeviceType? FilterType { get; set; }
    [Parameter] public EventCallback<DeviceType?> FilterTypeChanged { get; set; }

    [Parameter] public Func<Device, bool> FilterFunc { get; set; } = _ => true;

    // Action Callbacks
    [Parameter] public EventCallback<Device> OnBackupDevice { get; set; }
    [Parameter] public EventCallback<int> OnNavigateToBackups { get; set; }
    [Parameter] public EventCallback<Device> OnEditDevice { get; set; }
    [Parameter] public EventCallback<Device> OnDeleteDevice { get; set; }

    private async Task OnSearchChanged(string value)
    {
        SearchString = value;
        await SearchStringChanged.InvokeAsync(value);
    }

    private async Task OnFilterChanged(DeviceType? value)
    {
        FilterType = value;
        await FilterTypeChanged.InvokeAsync(value);
    }
}
