using HomeRecall.Persistence.Enums;
using MudBlazor;

namespace HomeRecall.Components;

public static class UiHelpers
{
    public static Color GetColorForDeviceType(DeviceType type)
    {
        return type switch
        {
            // DeviceType.Tasmota => Color.Warning,
            // DeviceType.Wled => Color.Error,
            // DeviceType.Shelly => Color.Info,
            // DeviceType.ShellyGen2 => Color.Info,
            // DeviceType.OpenDtu => Color.Success,
            // DeviceType.AiOnTheEdge => Color.Secondary,
            // DeviceType.Awtrix => Color.Tertiary,
            // DeviceType.OpenHasp => Color.Warning,
            _ => Color.Default
        };
    }

    public static string GetIconForDeviceType(DeviceType type)
    {
        return type switch
        {
            DeviceType.Tasmota => Icons.Material.Outlined.ToggleOn,
            DeviceType.Wled => Icons.Material.Outlined.ColorLens,
            DeviceType.Shelly => Icons.Material.Outlined.Router,
            DeviceType.ShellyGen2 => Icons.Material.Outlined.Router,
            DeviceType.OpenDtu => Icons.Material.Outlined.WbSunny,
            DeviceType.AiOnTheEdge => Icons.Material.Outlined.CameraAlt,
            DeviceType.Awtrix => Icons.Material.Outlined.Watch,
            DeviceType.OpenHasp => Icons.Material.Outlined.TouchApp,
            _ => Icons.Material.Outlined.DevicesOther
        };
    }

    public static long GetIpAddressSortValue(string? ipAddress)
    {
        if (System.Net.IPAddress.TryParse(ipAddress, out var parsedIp))
        {
            var bytes = parsedIp.GetAddressBytes();
            if (bytes.Length == 4)
            {
                // Returns a long constructed from the 4 bytes.
                // Cast to long happens before shift to prevent negative numbers.
                return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
            }
        }
        return -1;
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
