using System.Globalization;
using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeRecall.Components.Pages;

public partial class Settings : ComponentBase
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private string _currentLanguage = "auto";
    private AppSettings? _settings;

    protected override async Task OnInitializedAsync()
    {
        var culture = CultureInfo.CurrentCulture.Name;
        if (culture.StartsWith("de")) _currentLanguage = "de";
        else if (culture.StartsWith("en")) _currentLanguage = "en";

        _settings = await Context.Settings.FindAsync(1);
        if (_settings == null)
        {
            // Fallback should not happen due to seeding
            _settings = new AppSettings { Id = 1 };
            Context.Settings.Add(_settings);
            await Context.SaveChangesAsync();
        }
    }

    private void ChangeLanguage(string language)
    {
        _currentLanguage = language;
        var uri = new Uri(NavigationManager.Uri)
            .GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);

        NavigationManager.NavigateTo($"api/Culture/Set?culture={language}&redirectUri={Uri.EscapeDataString(uri)}", true);
    }

    private async Task SaveSettings()
    {
        if (_settings != null)
        {
            Context.Settings.Update(_settings);
            await Context.SaveChangesAsync();
            Snackbar.Add(L["Settings_Saved_Success"], Severity.Success);
        }
    }
}
