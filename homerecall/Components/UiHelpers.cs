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
}
