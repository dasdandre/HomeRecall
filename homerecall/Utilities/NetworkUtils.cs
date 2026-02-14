namespace HomeRecall.Utilities;

public static class NetworkUtils
{
    /// <summary>
    /// Normalizes a MAC address by removing non-alphanumeric characters and converting to uppercase.
    /// </summary>
    /// <param name="mac">The MAC address string to normalize.</param>
    /// <returns>A normalized MAC address string, or an empty string if the input is null or whitespace.</returns>
    public static string NormalizeMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
        return new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
    }
}
