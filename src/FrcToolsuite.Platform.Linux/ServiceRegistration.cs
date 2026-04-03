using FrcToolsuite.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Platform.Linux;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IPlatformService, LinuxPlatformService>();
        return services;
    }
}
