using HomeRecall.Persistence.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace HomeRecall.Components.Pages.BackupComponents;

public partial class BackupMobileList : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;

    [Parameter] public List<Backup> Backups { get; set; } = new();

    // Callbacks for parent component interaction
    [Parameter] public EventCallback<(Backup, string)> OnUpdateNote { get; set; }
    [Parameter] public EventCallback<Backup> OnToggleLock { get; set; }
    [Parameter] public EventCallback<Backup> OnDownloadBackup { get; set; }
    [Parameter] public EventCallback<Backup> OnDeleteBackup { get; set; }

    // Helper to determine if a backup is identical to its predecessor based on SHA1
    private bool IsDuplicateOfPrevious(Backup current)
    {
        // Check if the NEXT older backup has the same hash.
        // Since the list is typically ordered descending by date in UI, but we need to check logical predecessor.
        // We can do this in-memory since we load all backups for the device anyway.

        var allBackups = Backups.OrderByDescending(b => b.CreatedAt).ToList();
        if (allBackups == null) return false;

        var index = allBackups.IndexOf(current);
        if (index == -1 || index == allBackups.Count - 1) return false; // Not found or oldest item

        var previous = allBackups[index + 1];
        return previous.Sha1Checksum == current.Sha1Checksum;
    }
}
