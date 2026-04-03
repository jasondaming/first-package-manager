using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FrcToolsuite.Gui.Converters;

public class StringEqualConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && parameter is string p)
        {
            return string.Equals(s, p, StringComparison.Ordinal);
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
