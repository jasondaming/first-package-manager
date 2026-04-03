using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public class FeaturedBundle
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";
    public int PackageCount { get; set; }
}

public partial class HomePageViewModel : ObservableObject, IStateExportable
{
    [ObservableProperty]
    private string _welcomeMessage = "Welcome to FIRST Package Manager";

    public ObservableCollection<FeaturedBundle> FeaturedBundles { get; } = new()
    {
        new FeaturedBundle
        {
            Name = "FRC Starter Kit",
            Description = "Everything you need to start FRC development: WPILib, Driver Station, and more.",
            Icon = "\U0001F680",
            PackageCount = 8
        },
        new FeaturedBundle
        {
            Name = "Vision Processing",
            Description = "PhotonVision, Limelight tools, and OpenCV libraries for robot vision.",
            Icon = "\U0001F4F7",
            PackageCount = 5
        },
        new FeaturedBundle
        {
            Name = "Dashboard Essentials",
            Description = "Shuffleboard, Glass, AdvantageScope, and Elastic dashboard.",
            Icon = "\U0001F4CA",
            PackageCount = 4
        },
        new FeaturedBundle
        {
            Name = "Simulation Tools",
            Description = "Physics simulation, field visualization, and testing frameworks.",
            Icon = "\U0001F9EA",
            PackageCount = 3
        }
    };

    public string ExportStateJson()
    {
        var state = new
        {
            WelcomeMessage,
            FeaturedBundleCount = FeaturedBundles.Count
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
