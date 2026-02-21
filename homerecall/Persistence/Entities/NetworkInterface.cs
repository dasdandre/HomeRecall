using System.ComponentModel.DataAnnotations;

namespace HomeRecall.Persistence.Entities;

public class NetworkInterface
{
    public int Id { get; set; }

    [Required]
    public int DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    [Required]
    public string IpAddress { get; set; } = string.Empty;

    public Enums.NetworkInterfaceType Type { get; set; } = Enums.NetworkInterfaceType.Unknown;

    public string? Hostname { get; set; }

    private string? _macAddress;
    public string? MacAddress
    {
        get => _macAddress;
        set => _macAddress = HomeRecall.Utilities.NetworkUtils.NormalizeMac(value);
    }
}
