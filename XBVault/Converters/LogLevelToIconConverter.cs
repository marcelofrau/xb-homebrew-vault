using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using XBVault.Services;

namespace XBVault.Converters
{
    public class LogLevelToIconConverter : IValueConverter
    {
        private static readonly Bitmap TraceIcon = LoadIcon("logs-trace-16.png");
        private static readonly Bitmap DebugIcon = LoadIcon("logs-debug-16.png");
        private static readonly Bitmap InfoIcon = LoadIcon("logs-info-16.png");
        private static readonly Bitmap WarnIcon = LoadIcon("logs-warn-16.png");
        private static readonly Bitmap ErrorIcon = LoadIcon("logs-error-16.png");
        private static readonly Bitmap FatalIcon = LoadIcon("logs-fatal-16.png");

        private static Bitmap LoadIcon(string name)
        {
            var uri = new Uri($"avares://XBVault/Assets/Views/LogsView/{name}");
            return new Bitmap(AssetLoader.Open(uri));
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is LogLevel lvl)
            {
                return lvl switch
                {
                    LogLevel.Trace => TraceIcon,
                    LogLevel.Debug => DebugIcon,
                    LogLevel.Info  => InfoIcon,
                    LogLevel.Warn  => WarnIcon,
                    LogLevel.Error => ErrorIcon,
                    LogLevel.Fatal => FatalIcon,
                    _              => InfoIcon
                };
            }
            return InfoIcon;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
