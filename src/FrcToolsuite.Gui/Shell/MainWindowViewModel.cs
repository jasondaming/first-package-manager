using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
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

    public ObservableCollection<string> Programs { get; } = new() { "FRC", "FTC" };

    public ObservableCollection<string> Seasons { get; } = new() { "2026", "2025", "2024" };

    public HomePageViewModel HomePage { get; } = new();
    public BrowsePageViewModel BrowsePage { get; } = new();
    public InstalledPageViewModel InstalledPage { get; } = new();
    public UpdatesPageViewModel UpdatesPage { get; } = new();
    public SettingsPageViewModel SettingsPage { get; } = new();
    public ProfilesPageViewModel ProfilesPage { get; } = new();
    public UsbModePageViewModel UsbModePage { get; } = new();
    public HealthPageViewModel HealthPage { get; } = new();
    public PackageDetailPageViewModel PackageDetailPage { get; } = new();

    public MainWindowViewModel()
    {
        CurrentPageViewModel = HomePage;
    }

    [RelayCommand]
    private void NavigateTo(string pageName)
    {
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
            SelectedSeason
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
