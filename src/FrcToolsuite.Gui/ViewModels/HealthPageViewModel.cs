using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public partial class HealthPageViewModel : ObservableObject, IStateExportable
{
    [ObservableProperty]
    private string _title = "Environment Health";

    [ObservableProperty]
    private string _description = "Diagnose your FRC development environment. Verify that Java, compilers, network settings, and tool versions are correctly configured and compatible.";

    [ObservableProperty]
    private string _status = "Coming Soon";

    public string ExportStateJson()
    {
        var state = new
        {
            Title,
            Description,
            Status
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
