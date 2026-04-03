using FrcToolsuite.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Platform.macOS;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IPlatformService, MacPlatformService>();
        return services;
    }
}
