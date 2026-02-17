using System.Globalization;
using HomeRecall.Persistence;
using HomeRecall.Persistence.Entities;
using HomeRecall.Persistence.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using HomeRecall.Services;
using HomeRecall.Services.Strategies;

namespace HomeRecall.Components.Pages;

public partial class Settings : ComponentBase, IDisposable
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private BackupContext Context { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IMqttService MqttService { get; set; } = null!;
    [Inject] private IDataProtectionProvider DataProtectionProvider { get; set; } = null!;
    [Inject] private IEnumerable<IDeviceStrategy> Strategies { get; set; } = null!;

    private string _currentLanguage = "auto";
    private AppSettings? _settings;
    private string? _mqttPassword;
    private IDataProtector _protector = null!;
    private List<string> _excludedMqttTypes = new();
    private List<IMqttDeviceStrategy> _mqttCapableStrategies = new();

    protected override async Task OnInitializedAsync()
    {
        _protector = DataProtectionProvider.CreateProtector("MqttPassword");
        
        MqttService.StatusChanged += OnStatusChanged;

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

        if (!string.IsNullOrEmpty(_settings.MqttPasswordEncrypted))
        {
            // We don't decrypt back to UI for security, just show dots or leave empty
            _mqttPassword = "********"; 
        }

        _excludedMqttTypes = _settings.MqttExcludedDeviceTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
        _mqttCapableStrategies = Strategies.OfType<IMqttDeviceStrategy>().Where(s => s.MqttDiscoveryTopics.Any()).ToList();
    }

    private void OnStatusChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        MqttService.StatusChanged -= OnStatusChanged;
    }

    private void ChangeLanguage(string language)
    {
        _currentLanguage = language;
        var uri = new Uri(NavigationManager.Uri)
            .GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);

        NavigationManager.NavigateTo($"api/Culture/Set?culture={language}&redirectUri={Uri.EscapeDataString(uri)}", true);
    }

    private void ToggleMqttExclusion(DeviceType type)
    {
        var typeStr = type.ToString();
        if (_excludedMqttTypes.Contains(typeStr))
        {
            _excludedMqttTypes.Remove(typeStr);
        }
        else
        {
            _excludedMqttTypes.Add(typeStr);
        }
    }

    private async Task SaveSettings()
    {
        if (_settings != null)
        {
            if (!string.IsNullOrEmpty(_mqttPassword) && _mqttPassword != "********")
            {
                _settings.MqttPasswordEncrypted = _protector.Protect(_mqttPassword);
            }

            _settings.MqttExcludedDeviceTypes = string.Join(",", _excludedMqttTypes);

            Context.Settings.Update(_settings);
            await Context.SaveChangesAsync();
            
            // Trigger MQTT reconnect with new settings
            await MqttService.ReconnectAsync();

            Snackbar.Add(L["Settings_Saved_Success"], Severity.Success);
        }
    }
}
