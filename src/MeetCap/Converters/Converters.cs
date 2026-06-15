using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MeetCap.Models;

namespace MeetCap.Converters;

/// <summary>Friendly display text for the enums shown in combo boxes.</summary>
public sealed class EnumDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        RecordingQuality.Standard => "Standard",
        RecordingQuality.High => "High",
        RecordingQuality.Ultra => "Ultra",
        PlatformAction.Ask => "Ask each time",
        PlatformAction.AutoRecord => "Record automatically",
        PlatformAction.Ignore => "Ignore",
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}
