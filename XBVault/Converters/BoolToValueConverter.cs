using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace XBVault.Converters;

public class BoolToValueConverter : IValueConverter
{
    public object? TrueValue { get; set; }
    public object? FalseValue { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is true ? TrueValue : FalseValue;
        if (result is not null && targetType.IsInstanceOfType(result) == false)
        {
            try { return System.Convert.ChangeType(result, targetType, culture); }
            catch { }
        }
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
