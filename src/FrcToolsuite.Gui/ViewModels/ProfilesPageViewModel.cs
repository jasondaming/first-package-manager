using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public partial class ProfilesPageViewModel : ObservableObject, IStateExportable
{
    [ObservableProperty]
    private string _title = "Team Profiles";

    [ObservableProperty]
    private string _description = "Create and share team installation profiles. Export your tool configuration so teammates can replicate your exact setup with one click.";

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
