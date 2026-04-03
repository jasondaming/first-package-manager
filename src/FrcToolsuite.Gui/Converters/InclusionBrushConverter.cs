using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FrcToolsuite.Gui.Converters;

public class InclusionBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inclusion = value as string ?? string.Empty;
        return inclusion switch
        {
            "Required" => new SolidColorBrush(Color.Parse("#E57373")),
            "Default" => new SolidColorBrush(Color.Parse("#5B8DEF")),
            "Optional" => new SolidColorBrush(Color.Parse("#99AABB")),
            _ => new SolidColorBrush(Color.Parse("#99AABB"))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
