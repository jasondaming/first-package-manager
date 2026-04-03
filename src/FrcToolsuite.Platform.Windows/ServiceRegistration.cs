using FrcToolsuite.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Platform.Windows;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IPlatformService, WindowsPlatformService>();
        return services;
    }
}
