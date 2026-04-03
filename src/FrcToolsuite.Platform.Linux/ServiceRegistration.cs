using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Platform.Linux;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(IServiceCollection services)
    {
        // TODO: Register Linux-specific service implementations
        // - APT/DEB package integration
        // - Linux environment variable management (shell profiles)
        // - udev rules for roboRIO USB
        // - Linux-specific path resolution
        return services;
    }
}
