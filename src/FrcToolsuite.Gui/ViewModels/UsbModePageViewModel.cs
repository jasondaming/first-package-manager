using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public partial class UsbModePageViewModel : ObservableObject, IStateExportable
{
    [ObservableProperty]
    private string _title = "USB Offline Mode";

    [ObservableProperty]
    private string _description = "Download packages to a USB drive for offline installation at competition venues. Perfect for pit areas with limited or no internet access.";

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
