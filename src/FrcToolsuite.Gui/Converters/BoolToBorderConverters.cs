using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FrcToolsuite.Gui.Converters;

/// <summary>
/// Converts a boolean (IsSelected) to a border brush color.
/// Selected items get a blue border; unselected get a light dashed-style border.
/// </summary>
public class BoolToBorderBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        return isSelected
            ? new SolidColorBrush(Color.Parse("#5B8DEF"))
            : new SolidColorBrush(Color.Parse("#C8CDD6"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a boolean (IsSelected) to border thickness.
/// Selected items get a thicker border to show they are checked.
/// </summary>
public class BoolToBorderThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        return isSelected ? new Thickness(1.5) : new Thickness(1);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
