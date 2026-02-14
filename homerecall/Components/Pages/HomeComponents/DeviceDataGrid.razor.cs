using HomeRecall.Persistence.Entities;
using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages.HomeComponents;

public partial class DeviceDataGrid : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;

    [Parameter] public List<Device> Devices { get; set; } = new();
    [Parameter] public HashSet<Device> SelectedItems { get; set; } = new();
    [Parameter] public EventCallback<HashSet<Device>> SelectedItemsChanged { get; set; }

    [Parameter] public HashSet<int> IsBackingUp { get; set; } = new();
    [Parameter] public string SearchString { get; set; } = "";
    [Parameter] public EventCallback<string> SearchStringChanged { get; set; }

    [Parameter] public Func<Device, bool> QuickFilter { get; set; } = _ => true;
    [Parameter] public AppSettings? Settings { get; set; }

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

    private string FormatDate(DateTime? date)
    {
        if (!date.HasValue) return "-";

        if (Settings?.UseRelativeTime == true)
        {
            return date.Value.Humanize();
        }
        else
        {
            return date.Value.ToLocalTime().ToString("g");
        }
    }
}
