using System.CommandLine;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using FrcToolsuite.Cli.Commands;
using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Health;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Offline;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Platform;
using FrcToolsuite.Core.Registry;
using FrcToolsuite.Core.Update;

namespace FrcToolsuite.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = BuildServiceProvider();
        var rootCommand = new RootCommand("FIRST Robotics Package Manager");

        // Global options
        var programOption = new Option<string>(
            "--program",
            getDefaultValue: () => "frc",
            description: "Competition program (frc or ftc)");
        programOption.AddAlias("-p");

        var yearOption = new Option<int?>(
            "--year",
            description: "Season year (e.g. 2026)");
        yearOption.AddAlias("-y");

        var jsonOption = new Option<bool>(
            "--json",
            description: "Output results as JSON");

        var verboseOption = new Option<bool>(
            "--verbose",
            description: "Enable verbose logging");
        verboseOption.AddAlias("-v");

        var offlineOption = new Option<bool>(
            "--offline",
            description: "Operate in offline mode (use cached data only)");

        rootCommand.AddGlobalOption(programOption);
        rootCommand.AddGlobalOption(yearOption);
        rootCommand.AddGlobalOption(jsonOption);
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(offlineOption);

        // install
        var installCommand = new Command("install", "Install a package or bundle");
        var installPackageArg = new Argument<string>("package-or-bundle", () => string.Empty, "Package ID or bundle name to install");
        var installBundleOption = new Option<bool>("--bundle", "Treat the argument as a bundle name");
        var installYesOption = new Option<bool>("--yes", "Skip confirmation prompt");
        installYesOption.AddAlias("-Y");
        var installCsaOption = new Option<bool>("--csa", "Shortcut: install the CSA USB Toolkit bundle");
        var installVolunteerOption = new Option<bool>("--volunteer", "Shortcut: install the volunteer/team starter kit bundle");
        installCommand.AddArgument(installPackageArg);
        installCommand.AddOption(installBundleOption);
        installCommand.AddOption(installYesOption);
        installCommand.AddOption(installCsaOption);
        installCommand.AddOption(installVolunteerOption);
        installCommand.SetHandler(async (packageOrBundle, isBundle, yes, csa, volunteer) =>
        {
            var pm = services.GetRequiredService<IPackageManager>();
            if (csa)
            {
                Environment.ExitCode = await InstallCommand.ExecuteAsync(pm, "csa-usb-toolkit-2026", isBundle: true, autoConfirm: true);
            }
            else if (volunteer)
            {
                Environment.ExitCode = await InstallCommand.ExecuteAsync(pm, "frc-java-starter-2026", isBundle: true, autoConfirm: true);
            }
            else if (!string.IsNullOrEmpty(packageOrBundle))
            {
                Environment.ExitCode = await InstallCommand.ExecuteAsync(pm, packageOrBundle, isBundle, yes);
            }
            else
            {
                ConsoleHelper.WriteError("Please specify a package/bundle name, or use --csa / --volunteer.");
                Environment.ExitCode = 1;
            }
        }, installPackageArg, installBundleOption, installYesOption, installCsaOption, installVolunteerOption);

        // update
        var updateCommand = new Command("update", "Update installed packages");
        var updatePackageArg = new Argument<string?>("package-id", () => null, "Package ID to update (omit for interactive)");
        var updateAllOption = new Option<bool>("--all", "Update all installed packages");
        updateCommand.AddArgument(updatePackageArg);
        updateCommand.AddOption(updateAllOption);
        updateCommand.SetHandler(async (packageId, all) =>
        {
            var pm = services.GetRequiredService<IPackageManager>();
            Environment.ExitCode = await UpdateCommand.ExecuteAsync(pm, packageId, all);
        }, updatePackageArg, updateAllOption);

        // uninstall
        var uninstallCommand = new Command("uninstall", "Uninstall a package");
        var uninstallPackageArg = new Argument<string>("package-id", "Package ID to uninstall");
        uninstallCommand.AddArgument(uninstallPackageArg);
        uninstallCommand.SetHandler(async (packageId) =>
        {
            var pm = services.GetRequiredService<IPackageManager>();
            Environment.ExitCode = await UninstallCommand.ExecuteAsync(pm, packageId);
        }, uninstallPackageArg);

        // list
        var listCommand = new Command("list", "List packages");
        var listInstalledOption = new Option<bool>("--installed", "Show installed packages");
        var listAvailableOption = new Option<bool>("--available", "Show available packages");
        var listUpdatesOption = new Option<bool>("--updates", "Show packages with available updates");
        listCommand.AddOption(listInstalledOption);
        listCommand.AddOption(listAvailableOption);
        listCommand.AddOption(listUpdatesOption);
        listCommand.SetHandler(async (installed, available, updates) =>
        {
            var pm = services.GetRequiredService<IPackageManager>();
            var rc = services.GetRequiredService<IRegistryClient>();
            Environment.ExitCode = await ListCommand.ExecuteAsync(pm, rc, installed, available, updates);
        }, listInstalledOption, listAvailableOption, listUpdatesOption);

        // search
        var searchCommand = new Command("search", "Search for packages in the registry");
        var searchQueryArg = new Argument<string>("query", "Search query");
        searchCommand.AddArgument(searchQueryArg);
        searchCommand.SetHandler(async (query) =>
        {
            var rc = services.GetRequiredService<IRegistryClient>();
            Environment.ExitCode = await SearchCommand.ExecuteAsync(rc, query);
        }, searchQueryArg);

        // health
        var healthCommand = new Command("health", "Check installation health and integrity");
        var healthFixOption = new Option<bool>("--fix", "Attempt to automatically fix issues");
        var healthPackageOption = new Option<string?>("--package", "Check only a specific package");
        healthCommand.AddOption(healthFixOption);
        healthCommand.AddOption(healthPackageOption);
        healthCommand.SetHandler(async (fix, package_) =>
        {
            var hc = services.GetRequiredService<IHealthChecker>();
            Environment.ExitCode = await HealthCommand.ExecuteAsync(hc, fix, package_);
        }, healthFixOption, healthPackageOption);

        // sync-usb
        var syncUsbCommand = new Command("sync-usb", "Sync packages to a USB drive for offline installation");
        var syncDrivePathArg = new Argument<string>("drive-path", "Path to the USB drive");
        var syncBundleOption = new Option<string?>("--bundle", "Bundle to sync");
        var syncPlatformOption = new Option<string?>("--platform", "Target platform (windows, macos, linux)");
        syncUsbCommand.AddArgument(syncDrivePathArg);
        syncUsbCommand.AddOption(syncBundleOption);
        syncUsbCommand.AddOption(syncPlatformOption);
        syncUsbCommand.SetHandler(async (drivePath, bundle, platform) =>
        {
            var cm = services.GetRequiredService<IOfflineCacheManager>();
            var pm = services.GetRequiredService<IPackageManager>();
            var rc = services.GetRequiredService<IRegistryClient>();
            Environment.ExitCode = await SyncUsbCommand.ExecuteAsync(cm, pm, rc, drivePath, bundle);
        }, syncDrivePathArg, syncBundleOption, syncPlatformOption);

        // profile
        var profileCommand = new Command("profile", "Manage team installation profiles");

        var profileExportCommand = new Command("export", "Export current installation as a profile");
        var profileExportFileArg = new Argument<string?>("file", () => null, "Output file path (default: team-profile.json)");
        profileExportCommand.AddArgument(profileExportFileArg);
        profileExportCommand.SetHandler(async (file) =>
        {
            var pm = services.GetRequiredService<IPackageManager>();
            Environment.ExitCode = await ProfileCommand.ExecuteExportAsync(pm, file);
        }, profileExportFileArg);

        var profileImportCommand = new Command("import", "Import a team profile");
        var profileImportFileArg = new Argument<string?>("file", () => null, "Profile file path (default: team-profile.json)");
        profileImportCommand.AddArgument(profileImportFileArg);
        profileImportCommand.SetHandler(async (file) =>
        {
            Environment.ExitCode = await ProfileCommand.ExecuteImportAsync(file);
        }, profileImportFileArg);

        var profileApplyCommand = new Command("apply", "Apply a team profile to this machine");
        var profileApplyFileArg = new Argument<string?>("file", () => null, "Profile file path (default: team-profile.json)");
        profileApplyCommand.AddArgument(profileApplyFileArg);
        profileApplyCommand.SetHandler(async (file) =>
        {
            var pm = services.GetRequiredService<IPackageManager>();
            Environment.ExitCode = await ProfileCommand.ExecuteApplyAsync(pm, file);
        }, profileApplyFileArg);

        profileCommand.AddCommand(profileExportCommand);
        profileCommand.AddCommand(profileImportCommand);
        profileCommand.AddCommand(profileApplyCommand);

        // config
        var configCommand = new Command("config", "Manage configuration settings");

        var configSetCommand = new Command("set", "Set a configuration value");
        var configSetKeyArg = new Argument<string>("key", "Configuration key");
        var configSetValueArg = new Argument<string>("value", "Configuration value");
        configSetCommand.AddArgument(configSetKeyArg);
        configSetCommand.AddArgument(configSetValueArg);
        configSetCommand.SetHandler(async (key, value) =>
        {
            var sp = services.GetRequiredService<ISettingsProvider>();
            Environment.ExitCode = await ConfigCommand.ExecuteSetAsync(sp, key, value);
        }, configSetKeyArg, configSetValueArg);

        var configGetCommand = new Command("get", "Get a configuration value");
        var configGetKeyArg = new Argument<string>("key", "Configuration key");
        configGetCommand.AddArgument(configGetKeyArg);
        configGetCommand.SetHandler(async (key) =>
        {
            var sp = services.GetRequiredService<ISettingsProvider>();
            Environment.ExitCode = await ConfigCommand.ExecuteGetAsync(sp, key);
        }, configGetKeyArg);

        var configListCommand = new Command("list", "List all configuration values");
        configListCommand.SetHandler(async () =>
        {
            var sp = services.GetRequiredService<ISettingsProvider>();
            Environment.ExitCode = await ConfigCommand.ExecuteListAsync(sp);
        });

        configCommand.AddCommand(configSetCommand);
        configCommand.AddCommand(configGetCommand);
        configCommand.AddCommand(configListCommand);

        // self-update
        var selfUpdateCommand = new Command("self-update", "Update the FIRST Package Manager itself");
        selfUpdateCommand.SetHandler(async () =>
        {
            var updater = services.GetRequiredService<ISelfUpdater>();
            Environment.ExitCode = await SelfUpdateCommand.ExecuteAsync(updater);
        });

        // Add all subcommands to root
        rootCommand.AddCommand(installCommand);
        rootCommand.AddCommand(updateCommand);
        rootCommand.AddCommand(uninstallCommand);
        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(searchCommand);
        rootCommand.AddCommand(healthCommand);
        rootCommand.AddCommand(syncUsbCommand);
        rootCommand.AddCommand(profileCommand);
        rootCommand.AddCommand(configCommand);
        rootCommand.AddCommand(selfUpdateCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // HTTP client
        services.AddSingleton<HttpClient>();

        // Platform service
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FrcToolsuite.Platform.Windows.ServiceRegistration.AddPlatformServices(services);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            FrcToolsuite.Platform.Linux.ServiceRegistration.AddPlatformServices(services);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            FrcToolsuite.Platform.macOS.ServiceRegistration.AddPlatformServices(services);
        }
        else
        {
            services.AddSingleton<IPlatformService, StubPlatformService>();
        }

        // Configuration
        services.AddSingleton<ISettingsProvider, SettingsProvider>();

        // Registry
        services.AddSingleton<IRegistryClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            return new RegistryClient(httpClient);
        });

        // Download
        services.AddSingleton<IDownloadManager>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            return new DownloadManager(httpClient);
        });

        // Install engine
        services.AddSingleton<IInstallEngine>(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            return new InstallEngine(platform);
        });

        // Package manager
        services.AddSingleton<IPackageManager>(sp =>
        {
            var registry = sp.GetRequiredService<IRegistryClient>();
            var download = sp.GetRequiredService<IDownloadManager>();
            var install = sp.GetRequiredService<IInstallEngine>();
            var platform = sp.GetRequiredService<IPlatformService>();
            var settings = sp.GetRequiredService<ISettingsProvider>();
            return new PackageManager(registry, download, install, platform, settings);
        });

        // Offline cache manager
        services.AddSingleton<IOfflineCacheManager>(sp =>
        {
            var registry = sp.GetRequiredService<IRegistryClient>();
            var download = sp.GetRequiredService<IDownloadManager>();
            return new OfflineCacheManager(registry, download);
        });

        // Health checker
        services.AddSingleton<IHealthChecker>(sp =>
        {
            var pm = sp.GetRequiredService<IPackageManager>();
            var settings = sp.GetRequiredService<ISettingsProvider>();
            return new HealthChecker(pm, settings);
        });

        // Self-updater
        services.AddSingleton<ISelfUpdater>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var downloadManager = sp.GetRequiredService<IDownloadManager>();
            var platform = sp.GetRequiredService<IPlatformService>();
            return new SelfUpdater(httpClient, downloadManager, platform);
        });

        return services.BuildServiceProvider();
    }
}
