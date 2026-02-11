using MudBlazor;
using HomeRecall;

namespace HomeRecall.Components;

public static class UiHelpers
{
    public static Color GetColorForDeviceType(DeviceType type)
    {
        return type switch
        {
            DeviceType.Tasmota => Color.Warning,
            DeviceType.Wled => Color.Error,
            DeviceType.Shelly => Color.Info,
            DeviceType.ShellyGen2 => Color.Info,
            DeviceType.OpenDtu => Color.Success,
            DeviceType.AiOnTheEdge => Color.Secondary,
            DeviceType.Awtrix => Color.Tertiary,
            DeviceType.OpenHasp => Color.Warning,
            _ => Color.Default
        };
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / 1024.0 / 1024.0):F1} MB";
        return $"{(bytes / 1024.0 / 1024.0 / 1024.0):F1} GB";
    }
}
