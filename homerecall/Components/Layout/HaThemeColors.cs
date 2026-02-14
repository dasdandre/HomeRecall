using System.Text.Json.Serialization;

namespace HomeRecall.Components.Layout;

public class HaThemeColors
{
    [JsonPropertyName("isDarkMode")]
    public bool IsDarkMode { get; set; }
    
    [JsonPropertyName("primary")]
    public string? Primary { get; set; }
    
    [JsonPropertyName("secondary")]
    public string? Secondary { get; set; }
    
    [JsonPropertyName("background")]
    public string? Background { get; set; }
    
    [JsonPropertyName("surface")]
    public string? Surface { get; set; }
    
    [JsonPropertyName("textPrimary")]
    public string? TextPrimary { get; set; }
    
    [JsonPropertyName("textSecondary")]
    public string? TextSecondary { get; set; }
    
    [JsonPropertyName("appBarBackground")]
    public string? AppBarBackground { get; set; }
    
    [JsonPropertyName("appBarText")]
    public string? AppBarText { get; set; }
    
    [JsonPropertyName("drawerBackground")]
    public string? DrawerBackground { get; set; }
    
    [JsonPropertyName("drawerText")]
    public string? DrawerText { get; set; }

    [JsonPropertyName("success")]
    public string? Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public static MudBlazor.PaletteLight GetHaPaletteLight() => new()
    {
        Primary = "#03a9f4",
        Secondary = "#ff9800",
        AppbarBackground = "#fafafa",
        AppbarText = "#212121",
        Background = "#fafafa",
        Surface = "#ffffff",
        TextPrimary = "#212121",
        TextSecondary = "#727272",
        DrawerBackground = "#ffffff",
        DrawerText = "#212121"
    };

    public static MudBlazor.PaletteDark GetHaPaletteDark() => new()
    {
        Primary = "#03a9f4",
        Secondary = "#ff9800",
        AppbarBackground = "#111111", // Match background
        AppbarText = "#e1e1e1",
        Background = "#111111",
        Surface = "#1e1e1e",
        TextPrimary = "#e1e1e1",
        TextSecondary = "#b0b0b0",
        DrawerBackground = "#1e1e1e",
        DrawerText = "#e1e1e1"
    };
}
