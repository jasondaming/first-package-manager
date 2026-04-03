using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Platform;
using FrcToolsuite.Core.Registry;
using FrcToolsuite.Gui.Shell;
using FrcToolsuite.Gui.ViewModels;

namespace FrcToolsuite.Gui;

public static class ServiceConfiguration
{
    public static IServiceProvider Configure()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<ISettingsProvider, SettingsProvider>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Platform.Windows.ServiceRegistration.AddPlatformServices(services);
        }
        else
        {
            services.AddSingleton<IPlatformService, StubPlatformService>();
        }
        services.AddSingleton<IRegistryClient>(sp =>
            new RegistryClient(new HttpClient()));
        services.AddSingleton<IDownloadManager>(sp =>
            new DownloadManager(new HttpClient()));
        services.AddSingleton<IInstallEngine>(sp =>
            new InstallEngine(sp.GetRequiredService<IPlatformService>()));
        services.AddSingleton<IPackageManager, PackageManager>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>(sp =>
            new MainWindowViewModel(
                sp.GetRequiredService<IPackageManager>(),
                sp.GetRequiredService<IRegistryClient>()));
        services.AddTransient<HomePageViewModel>(sp =>
            new HomePageViewModel(
                sp.GetRequiredService<IPackageManager>(),
                sp.GetRequiredService<IRegistryClient>()));
        services.AddTransient<BrowsePageViewModel>(sp =>
            new BrowsePageViewModel(
                sp.GetRequiredService<IRegistryClient>()));
        services.AddTransient<InstalledPageViewModel>(sp =>
            new InstalledPageViewModel(
                sp.GetRequiredService<IPackageManager>()));
        services.AddTransient<UpdatesPageViewModel>(sp =>
            new UpdatesPageViewModel(
                sp.GetRequiredService<IPackageManager>()));
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<ProfilesPageViewModel>();
        services.AddTransient<UsbModePageViewModel>();
        services.AddTransient<HealthPageViewModel>();
        services.AddTransient<PackageDetailPageViewModel>();

        return services.BuildServiceProvider();
    }
}
