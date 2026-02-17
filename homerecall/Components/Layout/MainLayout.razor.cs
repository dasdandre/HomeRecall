using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;
using HomeRecall.Services;

namespace HomeRecall.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IMqttService MqttService { get; set; } = null!;

    private MudTheme _theme = new MudTheme();
    private bool _isDarkMode = false;
    private bool _isHaConnected = false;
    private MudThemeProvider _mudThemeProvider = null!;
    private int _activeTab = 0;
    private DotNetObjectReference<MainLayout>? _objRef;

    protected override async Task OnInitializedAsync()
    {
        // Define standard HA Colors
        _theme.PaletteLight = HaThemeColors.GetHaPaletteLight();
        _theme.PaletteDark = HaThemeColors.GetHaPaletteDark();

        NavigationManager.LocationChanged += OnLocationChanged;
        MqttService.StatusChanged += OnStatusChanged;
        
        UpdateActiveTab(NavigationManager.Uri);
        await Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _objRef = DotNetObjectReference.Create(this);

            // Try to hook into HA theme
            try
            {
                _isHaConnected = await JS.InvokeAsync<bool>("window.observeHaThemeChange", _objRef);
                if (_isHaConnected) StateHasChanged();
            }
            catch
            {
                // Ignore if JS not available yet or other error
            }

            // Only fallback to system preference if NOT in HA (or HA sync failed)
            if (!_isHaConnected && _mudThemeProvider != null)
            {
                try
                {
                    _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
                    await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
                    StateHasChanged();
                }
                catch (Exception)
                {
                    // Ignore JS Interop errors if prerendering or disconnected
                }
            }
        }
    }

    [JSInvokable]
    public void UpdateThemeFromJs(HaThemeColors colors)
    {
        _isDarkMode = colors.IsDarkMode;

        var targetPalette = _isDarkMode ? (Palette)_theme.PaletteDark : (Palette)_theme.PaletteLight;

        if (colors.Primary != null) targetPalette.Primary = colors.Primary;
        if (colors.Secondary != null) targetPalette.Secondary = colors.Secondary;
        if (colors.Background != null) targetPalette.Background = colors.Background;
        if (colors.Surface != null) targetPalette.Surface = colors.Surface;
        if (colors.AppBarBackground != null) targetPalette.AppbarBackground = colors.AppBarBackground;
        if (colors.AppBarText != null) targetPalette.AppbarText = colors.AppBarText;
        if (colors.TextPrimary != null) targetPalette.TextPrimary = colors.TextPrimary;
        if (colors.TextSecondary != null) targetPalette.TextSecondary = colors.TextSecondary;
        if (colors.DrawerBackground != null) targetPalette.DrawerBackground = colors.DrawerBackground;
        if (colors.DrawerText != null) targetPalette.DrawerText = colors.DrawerText;
        if (colors.Success != null) targetPalette.Success = colors.Success;
        if (colors.Error != null) targetPalette.Error = colors.Error;

        StateHasChanged();
    }

    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _isDarkMode = newValue;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateActiveTab(e.Location);
        StateHasChanged();
    }

    private void OnStatusChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void UpdateActiveTab(string url)
    {
        if (url.Contains("/settings", StringComparison.OrdinalIgnoreCase))
        {
            _activeTab = 1;
        }
        else if (url.EndsWith("/") || !url.Contains("/backups", StringComparison.OrdinalIgnoreCase))
        {
            _activeTab = 0;
        }
        // If on backups page, keep current or default to devices (0)
        else if (url.Contains("/backups", StringComparison.OrdinalIgnoreCase))
        {
            _activeTab = 0;
        }
    }

    private void OnTabChanged(int index)
    {
        _activeTab = index;
        if (index == 0) NavigationManager.NavigateTo("");
        else if (index == 1) NavigationManager.NavigateTo("settings");
    }

    public void Dispose()
    {
        _objRef?.Dispose();
        NavigationManager.LocationChanged -= OnLocationChanged;
        MqttService.StatusChanged -= OnStatusChanged;
    }
}
