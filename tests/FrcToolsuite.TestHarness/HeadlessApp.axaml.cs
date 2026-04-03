using Avalonia;
using Avalonia.Markup.Xaml;

namespace FrcToolsuite.TestHarness;

public partial class HeadlessApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
