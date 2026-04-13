using Avalonia.Controls;
using FrcToolsuite.Gui.Shell;
using FrcToolsuite.Gui.ViewModels;
using FrcToolsuite.Gui.Views;

namespace FrcToolsuite.TestHarness;

public static class PageFactory
{
    private static readonly Dictionary<string, Func<(Control View, object ViewModel)>> Pages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Home"] = () =>
        {
            var vm = new HomePageViewModel();
            var view = new HomePage { DataContext = vm };
            return (view, vm);
        },
        ["Browse"] = () =>
        {
            var vm = new BrowsePageViewModel();
            var view = new BrowsePage { DataContext = vm };
            return (view, vm);
        },
        ["Installed"] = () =>
        {
            var vm = new InstalledPageViewModel();
            var view = new InstalledPage { DataContext = vm };
            return (view, vm);
        },
        ["Updates"] = () =>
        {
            var vm = new UpdatesPageViewModel();
            var view = new UpdatesPage { DataContext = vm };
            return (view, vm);
        },
        ["Settings"] = () =>
        {
            var vm = new SettingsPageViewModel();
            var view = new SettingsPage { DataContext = vm };
            return (view, vm);
        },
        ["Profiles"] = () =>
        {
            var vm = new ProfilesPageViewModel();
            var view = new ProfilesPage { DataContext = vm };
            return (view, vm);
        },
        ["UsbMode"] = () =>
        {
            var vm = new UsbModePageViewModel();
            var view = new UsbModePage { DataContext = vm };
            return (view, vm);
        },
        ["Health"] = () =>
        {
            var vm = new HealthPageViewModel();
            var view = new HealthPage { DataContext = vm };
            return (view, vm);
        },
        ["PackageDetail"] = () =>
        {
            var vm = new PackageDetailPageViewModel();
            var view = new PackageDetailPage { DataContext = vm };
            return (view, vm);
        },
        ["FirstRunWizard"] = () =>
        {
            var vm = new FirstRunWizardViewModel();
            var view = new FirstRunWizard { DataContext = vm };
            return (view, vm);
        },
        ["FirstRunWizardStep2"] = () =>
        {
            var vm = new FirstRunWizardViewModel();
            vm.GoNextCommand.Execute(null);
            var view = new FirstRunWizard { DataContext = vm };
            return (view, vm);
        },
        ["MainWindow"] = () =>
        {
            var vm = new MainWindowViewModel();
            var view = new MainWindow { DataContext = vm };
            return (view, vm);
        },
    };

    public static (Control View, object ViewModel) Create(string pageName)
    {
        if (Pages.TryGetValue(pageName, out var factory))
        {
            return factory();
        }

        throw new ArgumentException(
            $"Unknown page '{pageName}'. Available pages: {string.Join(", ", Pages.Keys)}",
            nameof(pageName));
    }

    public static IReadOnlyList<string> AvailablePages => Pages.Keys.ToList().AsReadOnly();
}
