using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FrcToolsuite.Gui.Converters;

public class BoolToExpandIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "\u25b4" : "\u25be";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
