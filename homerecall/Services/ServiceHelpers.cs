using System.Linq;

namespace HomeRecall.Services;

public static class ServiceHelpers
{
    public static string NormalizeMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
        return new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
    }
}