using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Health;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;
using FrcToolsuite.Gui.ViewModels;

namespace FrcToolsuite.Gui.Shell;

public partial class MainWindowViewModel : ObservableObject, IStateExportable
{
    [ObservableProperty]
    private string _selectedPage = "Home";

    [ObservableProperty]
    private string _selectedProgram = "FRC";

    [ObservableProperty]
    private string _selectedSeason = "2026";

    [ObservableProperty]
    private object? _currentPageViewModel;

    [ObservableProperty]
    private bool _showFirstRunWizard;

    public ObservableCollection<string> Programs { get; } = new() { "FRC", "FTC" };

    public ObservableCollection<string> Seasons { get; } = new() { "2026", "2025", "2024" };

    public HomePageViewModel HomePage { get; }
    public BrowsePageViewModel BrowsePage { get; }
    public InstalledPageViewModel InstalledPage { get; }
    public UpdatesPageViewModel UpdatesPage { get; }
    public SettingsPageViewModel SettingsPage { get; } = new();
    public ProfilesPageViewModel ProfilesPage { get; } = new();
    public UsbModePageViewModel UsbModePage { get; } = new();
    public HealthPageViewModel HealthPage { get; }
    public PackageDetailPageViewModel PackageDetailPage { get; }
    public FirstRunWizardViewModel FirstRunWizard { get; }

    public MainWindowViewModel()
        : this(null, null, null)
    {
    }

    public MainWindowViewModel(IPackageManager? packageManager, IRegistryClient? registry, IHealthChecker? healthChecker = null)
    {
        HomePage = new HomePageViewModel(packageManager, registry, NavigateTo);
        BrowsePage = new BrowsePageViewModel(registry, packageManager);
        InstalledPage = new InstalledPageViewModel(packageManager);
        UpdatesPage = new UpdatesPageViewModel(packageManager);
        PackageDetailPage = new PackageDetailPageViewModel(NavigateTo);
        HealthPage = new HealthPageViewModel(healthChecker);
        FirstRunWizard = new FirstRunWizardViewModel(packageManager, DismissFirstRunWizard);
        CurrentPageViewModel = HomePage;

        // Show first-run wizard if no previous install is detected
        ShowFirstRunWizard = !HasPreviousInstall();
    }

    private static bool HasPreviousInstall()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".frctoolsuite",
                "settings.json");
            return File.Exists(settingsPath);
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void DismissFirstRunWizard()
    {
        ShowFirstRunWizard = false;
    }

    [RelayCommand]
    private void NavigateTo(string pageName)
    {
        if (pageName == "FirstRun")
        {
            ShowFirstRunWizard = true;
            return;
        }

        SelectedPage = pageName;
        CurrentPageViewModel = pageName switch
        {
            "Home" => HomePage,
            "Browse" => BrowsePage,
            "Installed" => InstalledPage,
            "Updates" => UpdatesPage,
            "Settings" => SettingsPage,
            "Profiles" => ProfilesPage,
            "USBMode" => UsbModePage,
            "Health" => HealthPage,
            "PackageDetail" => PackageDetailPage,
            _ => HomePage
        };
    }

    public string ExportStateJson()
    {
        var state = new
        {
            SelectedPage,
            SelectedProgram,
            SelectedSeason,
            ShowFirstRunWizard
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
