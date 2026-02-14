using HomeRecall.Persistence.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace HomeRecall.Components.Pages.BackupComponents;

public partial class BackupTable : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;

    [Parameter] public List<Backup> Backups { get; set; } = new();

    [Parameter] public HashSet<Backup> SelectedItems { get; set; } = new();
    [Parameter] public EventCallback<HashSet<Backup>> SelectedItemsChanged { get; set; }

    [Parameter] public bool Loading { get; set; }

    [Parameter] public EventCallback<(Backup, string)> OnUpdateNote { get; set; }
    [Parameter] public EventCallback<Backup> OnToggleLock { get; set; }
    [Parameter] public EventCallback<Backup> OnDownloadBackup { get; set; }
    [Parameter] public EventCallback<Backup> OnDeleteBackup { get; set; }

    private bool IsDuplicateOfPrevious(Backup current)
    {
        var allBackups = Backups.OrderByDescending(b => b.CreatedAt).ToList();
        if (allBackups == null) return false;

        var index = allBackups.IndexOf(current);
        if (index == -1 || index == allBackups.Count - 1) return false;

        var previous = allBackups[index + 1];
        return previous.Sha1Checksum == current.Sha1Checksum;
    }
}
