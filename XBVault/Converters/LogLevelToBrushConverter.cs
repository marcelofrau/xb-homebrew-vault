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
                    LogLevel.Debug => Brushes.Gray,
                    LogLevel.Info => (IBrush)Avalonia.Media.Brushes.LightGreen,
                    LogLevel.Warn => (IBrush)Avalonia.Media.Brushes.Orange,
                    LogLevel.Error => (IBrush)Avalonia.Media.Brushes.Red,
                    _ => Avalonia.Media.Brushes.White
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
