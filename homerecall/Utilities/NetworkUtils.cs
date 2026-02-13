namespace HomeRecall.Utilities;

public static class NetworkUtils
{
    public static string NormalizeMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
        return new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
    }
}
