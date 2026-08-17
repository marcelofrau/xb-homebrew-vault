using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

#nullable enable

namespace XBVault.Converters;

public class BoolToHighlightBgConverter : IValueConverter
{
    private static readonly IBrush _highlightBg = new SolidColorBrush(0x209ACA3C);
    private static readonly IBrush _transparent = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _highlightBg : _transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
