using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

#nullable enable
using System.Globalization;
using XBVault.Services;

namespace XBVault.Converters
{
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is LogLevel lvl)
            {
                return lvl switch
                {
                    LogLevel.Trace => new SolidColorBrush(Color.Parse("#8AE234")),
                    LogLevel.Debug => new SolidColorBrush(Color.Parse("#729FCF")),
                    LogLevel.Info => new SolidColorBrush(Color.Parse("#EEEEEC")),
                    LogLevel.Warn => new SolidColorBrush(Color.Parse("#FCE94F")),
                    LogLevel.Error => new SolidColorBrush(Color.Parse("#EF2929")),
                    LogLevel.Fatal => new SolidColorBrush(Color.Parse("#F57900")),
                    _ => new SolidColorBrush(Color.Parse("#F0F0F0"))
                };
            }
            return Avalonia.Media.Brushes.White;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
