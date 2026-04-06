using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Gui.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Gui;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceConfiguration.Configure();

        // Apply saved theme
        var settingsProvider = Services.GetRequiredService<ISettingsProvider>();
        var settings = settingsProvider.LoadAsync().GetAwaiter().GetResult();
        SetTheme(settings.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void SetTheme(string theme)
    {
        if (Current is not App app) return;

        app.RequestedThemeVariant = theme.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
