using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Platform.Windows;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(IServiceCollection services)
    {
        // TODO: Register Windows-specific service implementations
        // - Windows installer integration (MSI/MSIX)
        // - Windows environment variable management
        // - Windows shortcut creation
        // - Windows-specific path resolution
        return services;
    }
}
