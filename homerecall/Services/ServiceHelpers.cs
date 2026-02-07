using System.Linq;

namespace HomeRecall.Services;

public static class ServiceHelpers
{
        public static string NormalizeMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
        return new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
    }
    
    public static void AddDeviceStrategies(this IServiceCollection services)
    {
        services.AddScoped<IDeviceStrategy, TasmotaStrategy>();
        services.AddScoped<IDeviceStrategy, WledStrategy>();
        services.AddScoped<IDeviceStrategy, ShellyStrategy>();
        services.AddScoped<IDeviceStrategy, ShellyGen2Strategy>();
        services.AddScoped<IDeviceStrategy, OpenDtuStrategy>();
        services.AddScoped<IDeviceStrategy, AiOnTheEdgeStrategy>();
        services.AddScoped<IDeviceStrategy, AwtrixStrategy>();
        services.AddScoped<IDeviceStrategy, OpenHaspStrategy>();
    }
}