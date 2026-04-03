using System.CommandLine;

namespace FrcToolsuite.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
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
        var installPackageArg = new Argument<string>("package-or-bundle", "Package ID or bundle name to install");
        var installBundleOption = new Option<bool>("--bundle", "Treat the argument as a bundle name");
        var installYearOption = new Option<int?>("--year", "Override season year for this install");
        installCommand.AddArgument(installPackageArg);
        installCommand.AddOption(installBundleOption);
        installCommand.AddOption(installYearOption);
        installCommand.SetHandler((packageOrBundle, isBundle, year) =>
        {
            Console.WriteLine("Not yet implemented");
        }, installPackageArg, installBundleOption, installYearOption);

        // update
        var updateCommand = new Command("update", "Update installed packages");
        var updatePackageArg = new Argument<string?>("package-id", () => null, "Package ID to update (omit for interactive)");
        var updateAllOption = new Option<bool>("--all", "Update all installed packages");
        updateCommand.AddArgument(updatePackageArg);
        updateCommand.AddOption(updateAllOption);
        updateCommand.SetHandler((packageId, all) =>
        {
            Console.WriteLine("Not yet implemented");
        }, updatePackageArg, updateAllOption);

        // uninstall
        var uninstallCommand = new Command("uninstall", "Uninstall a package");
        var uninstallPackageArg = new Argument<string>("package-id", "Package ID to uninstall");
        uninstallCommand.AddArgument(uninstallPackageArg);
        uninstallCommand.SetHandler((packageId) =>
        {
            Console.WriteLine("Not yet implemented");
        }, uninstallPackageArg);

        // list
        var listCommand = new Command("list", "List packages");
        var listInstalledOption = new Option<bool>("--installed", "Show installed packages");
        var listAvailableOption = new Option<bool>("--available", "Show available packages");
        var listUpdatesOption = new Option<bool>("--updates", "Show packages with available updates");
        listCommand.AddOption(listInstalledOption);
        listCommand.AddOption(listAvailableOption);
        listCommand.AddOption(listUpdatesOption);
        listCommand.SetHandler((installed, available, updates) =>
        {
            Console.WriteLine("Not yet implemented");
        }, listInstalledOption, listAvailableOption, listUpdatesOption);

        // search
        var searchCommand = new Command("search", "Search for packages in the registry");
        var searchQueryArg = new Argument<string>("query", "Search query");
        searchCommand.AddArgument(searchQueryArg);
        searchCommand.SetHandler((query) =>
        {
            Console.WriteLine("Not yet implemented");
        }, searchQueryArg);

        // health
        var healthCommand = new Command("health", "Check installation health and integrity");
        var healthFixOption = new Option<bool>("--fix", "Attempt to automatically fix issues");
        var healthPackageOption = new Option<string?>("--package", "Check only a specific package");
        healthCommand.AddOption(healthFixOption);
        healthCommand.AddOption(healthPackageOption);
        healthCommand.SetHandler((fix, package_) =>
        {
            Console.WriteLine("Not yet implemented");
        }, healthFixOption, healthPackageOption);

        // sync-usb
        var syncUsbCommand = new Command("sync-usb", "Sync packages to a USB drive for offline installation");
        var syncDrivePathArg = new Argument<string>("drive-path", "Path to the USB drive");
        var syncBundleOption = new Option<string?>("--bundle", "Bundle to sync");
        var syncPlatformOption = new Option<string?>("--platform", "Target platform (windows, macos, linux)");
        syncUsbCommand.AddArgument(syncDrivePathArg);
        syncUsbCommand.AddOption(syncBundleOption);
        syncUsbCommand.AddOption(syncPlatformOption);
        syncUsbCommand.SetHandler((drivePath, bundle, platform) =>
        {
            Console.WriteLine("Not yet implemented");
        }, syncDrivePathArg, syncBundleOption, syncPlatformOption);

        // profile
        var profileCommand = new Command("profile", "Manage team installation profiles");

        var profileExportCommand = new Command("export", "Export current installation as a profile");
        profileExportCommand.SetHandler(() =>
        {
            Console.WriteLine("Not yet implemented");
        });

        var profileImportCommand = new Command("import", "Import a team profile");
        profileImportCommand.SetHandler(() =>
        {
            Console.WriteLine("Not yet implemented");
        });

        var profileApplyCommand = new Command("apply", "Apply a team profile to this machine");
        profileApplyCommand.SetHandler(() =>
        {
            Console.WriteLine("Not yet implemented");
        });

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
        configSetCommand.SetHandler((key, value) =>
        {
            Console.WriteLine("Not yet implemented");
        }, configSetKeyArg, configSetValueArg);

        var configGetCommand = new Command("get", "Get a configuration value");
        var configGetKeyArg = new Argument<string>("key", "Configuration key");
        configGetCommand.AddArgument(configGetKeyArg);
        configGetCommand.SetHandler((key) =>
        {
            Console.WriteLine("Not yet implemented");
        }, configGetKeyArg);

        var configListCommand = new Command("list", "List all configuration values");
        configListCommand.SetHandler(() =>
        {
            Console.WriteLine("Not yet implemented");
        });

        configCommand.AddCommand(configSetCommand);
        configCommand.AddCommand(configGetCommand);
        configCommand.AddCommand(configListCommand);

        // self-update
        var selfUpdateCommand = new Command("self-update", "Update the FIRST Package Manager itself");
        selfUpdateCommand.SetHandler(() =>
        {
            Console.WriteLine("Not yet implemented");
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
}
