using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace XBVault.Helpers;

/// <summary>
/// Scales window content down (or up, via user preference) so it always fits
/// the screen work area regardless of resolution or DPI scaling.
/// Only touches the window when it would not fit at the current scale —
/// normal-sized screens keep the exact same layout.
/// </summary>
public static class WindowFitHelper
{
    private const double Margin = 8;

    private sealed class WindowInfo
    {
        public Control? Original { get; set; }
        public double DesignWidth { get; set; }
        public double DesignHeight { get; set; }
        public double MinWidth { get; set; }
        public double MinHeight { get; set; }
    }

    private static readonly ConditionalWeakTable<Window, WindowInfo> _info = new();

    /// <summary>
    /// Fits <paramref name="window"/> to its screen work area, applying the user's
    /// UI scale preference. Idempotent — safe to call on every open / setting change.
    /// </summary>
    public static void ApplyScale(Window window, double userScale = 1.0)
    {
        if (double.IsNaN(userScale) || userScale <= 0)
            userScale = 1.0;

        var info = _info.GetValue(window, static w => new WindowInfo
        {
            Original = w.Content as Control,
            DesignWidth = w.Width,
            DesignHeight = w.Height,
            MinWidth = w.MinWidth,
            MinHeight = w.MinHeight
        });

        if (info.Original is null)
            return;

        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null)
            return;

        double availW = screen.WorkingArea.Width / screen.Scaling - Margin;
        double availH = screen.WorkingArea.Height / screen.Scaling - Margin;
        if (availW <= 0 || availH <= 0)
            return;

        double fit = Math.Min(availW / info.DesignWidth, availH / info.DesignHeight);
        double scale = Math.Min(userScale, fit);

        if (userScale <= 1.0 && scale >= 1.0)
        {
            if (window.Content is LayoutTransformControl wrapped)
            {
                wrapped.Child = null;
                if (info.Original is { } original)
                {
                    original.RenderTransform = null;
                    original.RenderTransformOrigin = default;
                }
                window.Content = info.Original;
                RestoreSize(window, info);
            }
            return;
        }

        window.Width = info.DesignWidth * scale;
        window.Height = info.DesignHeight * scale;
        window.MinWidth = Math.Min(info.MinWidth, window.Width);
        window.MinHeight = Math.Min(info.MinHeight, window.Height);

        if (window.Content is LayoutTransformControl ltc)
        {
            ltc.LayoutTransform = new ScaleTransform(scale, scale);
        }
        else
        {
            window.Content = null;
            window.Content = new LayoutTransformControl
            {
                Child = info.Original,
                LayoutTransform = new ScaleTransform(scale, scale)
            };
        }

        CenterOnScreen(window, screen);
    }

    /// <summary>Design size (before any fit scaling) for the window, if known.</summary>
    public static (double Width, double Height) GetDesignSize(Window window)
    {
        if (_info.TryGetValue(window, out var info))
            return (info.DesignWidth, info.DesignHeight);
        return (window.Width, window.Height);
    }

    private static void RestoreSize(Window window, WindowInfo info)
    {
        window.Width = info.DesignWidth;
        window.Height = info.DesignHeight;
        window.MinWidth = info.MinWidth;
        window.MinHeight = info.MinHeight;
    }

    private static void CenterOnScreen(Window window, Screen screen)
    {
        var wa = screen.WorkingArea;
        int x = wa.X + (int)((wa.Width - window.Width * screen.Scaling) / 2);
        int y = wa.Y + (int)((wa.Height - window.Height * screen.Scaling) / 2);
        window.Position = new PixelPoint(x, y);
    }
}
