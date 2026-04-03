using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using FrcToolsuite.Core;
using FrcToolsuite.TestHarness;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "list":
        return RunList();

    case "screenshot":
        return RunScreenshot(args);

    case "state":
        return RunState(args);

    default:
        Console.Error.WriteLine($"Unknown command: '{command}'");
        PrintUsage();
        return 1;
}

static int RunList()
{
    Console.WriteLine("Available pages:");
    foreach (var page in PageFactory.AvailablePages)
    {
        Console.WriteLine($"  {page}");
    }
    return 0;
}

static int RunScreenshot(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: screenshot <PageName> [--width 1280] [--height 720] [--output path.png]");
        return 1;
    }

    var pageName = args[1];
    int width = 1280;
    int height = 720;
    string output = $"{pageName}.png";

    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--width" when i + 1 < args.Length:
                width = int.Parse(args[++i]);
                break;
            case "--height" when i + 1 < args.Length:
                height = int.Parse(args[++i]);
                break;
            case "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
            case "--state" when i + 1 < args.Length:
                // Reserved for future use: load state from JSON before rendering
                i++;
                break;
        }
    }

    InitializeHeadlessApp();

    try
    {
        var (view, _) = PageFactory.Create(pageName);
        if (view is Window window)
        {
            ScreenshotRenderer.RenderWindowToFile(window, width, height, output);
        }
        else
        {
            ScreenshotRenderer.RenderToFile(view, width, height, output);
        }
        return 0;
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int RunState(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: state <PageName> [--output state.json]");
        return 1;
    }

    var pageName = args[1];
    string output = $"{pageName}-state.json";

    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--output" when i + 1 < args.Length:
                output = args[++i];
                break;
        }
    }

    InitializeHeadlessApp();

    try
    {
        var (_, viewModel) = PageFactory.Create(pageName);

        if (viewModel is IStateExportable exportable)
        {
            var json = exportable.ExportStateJson();
            File.WriteAllText(output, json);
            Console.WriteLine($"State written to {output}");
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"ViewModel for '{pageName}' does not implement IStateExportable.");
            return 1;
        }
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static void InitializeHeadlessApp()
{
    AppBuilder.Configure<HeadlessApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
        .UseSkia()
        .SetupWithoutStarting();
}

static void PrintUsage()
{
    Console.WriteLine("FrcToolsuite Test Harness");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  testharness list");
    Console.WriteLine("  testharness screenshot <PageName> [--width 1280] [--height 720] [--output path.png] [--state state.json]");
    Console.WriteLine("  testharness state <PageName> [--output state.json]");
}
