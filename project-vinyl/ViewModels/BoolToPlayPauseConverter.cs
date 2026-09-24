using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ProjectVinyl.ViewModels;

public class BoolToPlayPauseConverter : IValueConverter
{
    public static readonly BoolToPlayPauseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
            return isPlaying ? "⏸" : "▶";
        return "▶";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}