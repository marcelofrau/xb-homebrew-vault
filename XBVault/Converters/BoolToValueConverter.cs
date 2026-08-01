using System;
using System.Globalization;
using Avalonia.Data.Converters;
using XBVault.Services;

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
            catch (Exception ex)
            {
                // Value type not convertible to targetType — fall back to raw value, don't break the binding
                Logger.Trace($"BoolToValueConverter: ChangeType failed for {result.GetType().Name} → {targetType.Name} — {ex.Message}");
            }
        }
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
