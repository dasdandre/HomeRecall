using HomeRecall.Services;
using HomeRecall.Services.Strategies;

namespace HomeRecall.Extensions;

public static class ServiceCollectionExtensions
{
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
