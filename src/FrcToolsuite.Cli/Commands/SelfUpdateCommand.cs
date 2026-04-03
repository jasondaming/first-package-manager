using System.Reflection;
using FrcToolsuite.Cli.Output;

namespace FrcToolsuite.Cli.Commands;

public static class SelfUpdateCommand
{
    public static Task<int> ExecuteAsync()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
        ConsoleHelper.WriteInfo($"FIRST Package Manager v{version}");
        ConsoleHelper.WriteInfo("Self-update is not yet implemented.");
        ConsoleHelper.WriteInfo("Check https://github.com/first-toolsuite/first-package-manager for the latest release.");
        return Task.FromResult(0);
    }
}
