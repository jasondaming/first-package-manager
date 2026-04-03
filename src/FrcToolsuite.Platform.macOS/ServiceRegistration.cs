using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Platform.macOS;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(IServiceCollection services)
    {
        // TODO: Register macOS-specific service implementations
        // - DMG/PKG installer integration
        // - macOS environment variable management (shell profiles)
        // - macOS Application bundle support
        // - macOS-specific path resolution
        return services;
    }
}
