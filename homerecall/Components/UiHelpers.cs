using HomeRecall.Persistence.Enums;
using MudBlazor;

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

    public static string GetStyleForDeviceSource(DeviceSource source)
    {
        return source switch
        {
            DeviceSource.Manual => "color: #1b5e20; background-color: rgba(27, 94, 32, 0.12);", // Dark green
            DeviceSource.IpScan => "color: #2e7d32; background-color: rgba(46, 125, 50, 0.12);", // Green
            DeviceSource.Mqtt => "color: #e65100; background-color: rgba(230, 81, 0, 0.12);",    // Orange
            DeviceSource.Mdns => "color: #6a1b9a; background-color: rgba(106, 27, 154, 0.12);",  // Purple
            _ => ""
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
