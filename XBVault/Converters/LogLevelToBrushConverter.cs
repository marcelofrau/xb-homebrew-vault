using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
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
                    LogLevel.Trace => new SolidColorBrush(Color.Parse("#8B8D91")),
                    LogLevel.Debug => new SolidColorBrush(Color.Parse("#5A5C60")),
                    LogLevel.Info  => new SolidColorBrush(Color.Parse("#2ECC71")),
                    LogLevel.Warn  => new SolidColorBrush(Color.Parse("#F39C12")),
                    LogLevel.Error => new SolidColorBrush(Color.Parse("#E74C3C")),
                    LogLevel.Fatal => new SolidColorBrush(Color.Parse("#E74C3C")),
                    _             => new SolidColorBrush(Color.Parse("#F0F0F0"))
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
