using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XBVault.Views;

public class PerformanceChart : Control
{
    private readonly List<double> _values = [];
    private const int MaxPoints = 60;

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<PerformanceChart, IBrush?>(nameof(Stroke), Brushes.Lime);

    public static readonly StyledProperty<string> CurrentValueProperty =
        AvaloniaProperty.Register<PerformanceChart, string>(nameof(CurrentValue), "0%");

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PerformanceChart, string>(nameof(Title), "");

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public string CurrentValue
    {
        get => GetValue(CurrentValueProperty);
        set => SetValue(CurrentValueProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public void AddValue(double value)
    {
        _values.Add(value);
        if (_values.Count > MaxPoints)
            _values.RemoveAt(0);
        InvalidateVisual();
    }

    public void Clear()
    {
        _values.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var bg = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
        context.FillRectangle(bg, new Rect(0, 0, w, h));

        var stroke = Stroke ?? Brushes.Lime;
        var linePen = new Pen(stroke, 1.5);
        var brightLine = new Pen(stroke, 2);
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), 0.5);
        var textFg = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        var padding = 8;
        var drawW = w - padding * 2;
        var drawH = h - padding * 2;

        var typeface = new Typeface("Segoe UI", weight: FontWeight.SemiBold);

        for (int i = 0; i <= 4; i++)
        {
            var y = padding + drawH * (1 - i / 4.0);
            context.DrawLine(gridPen, new Point(padding, y), new Point(padding + drawW, y));
        }

        if (!string.IsNullOrEmpty(Title))
        {
            var titleText = new FormattedText(Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, textFg);
            context.DrawText(titleText, new Point(padding + 4, padding + 2));
        }
        if (!string.IsNullOrEmpty(CurrentValue))
        {
            var valText = new FormattedText(CurrentValue, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, textFg);
            context.DrawText(valText, new Point(padding + drawW - valText.Width - 22, padding + 2));
        }

        if (_values.Count < 2) return;

        double maxVal = 100;
        foreach (var v in _values)
            if (v > maxVal) maxVal = v;
        if (maxVal <= 0) maxVal = 1;

        var points = new Point[_values.Count];
        for (int i = 0; i < _values.Count; i++)
        {
            var x = padding + drawW * (1 - (double)(_values.Count - 1 - i) / Math.Max(_values.Count - 1, 1));
            var y = padding + drawH * (1 - Math.Clamp(_values[i] / maxVal, 0, 1));
            points[i] = new Point(x, y);
        }

        // fill under curve
        var fillColor = (stroke as SolidColorBrush)?.Color ?? Colors.Lime;
        var fillBrush = new SolidColorBrush(Color.FromArgb(0x30, fillColor.R, fillColor.G, fillColor.B));
        var fillGeo = new StreamGeometry();
        using (var fillCtx = fillGeo.Open())
        {
            fillCtx.BeginFigure(points[0], true);
            for (int i = 1; i < points.Length; i++)
                fillCtx.LineTo(points[i]);
            fillCtx.LineTo(new Point(points[^1].X, padding + drawH));
            fillCtx.LineTo(new Point(points[0].X, padding + drawH));
            fillCtx.EndFigure(true);
        }
        context.DrawGeometry(fillBrush, null, fillGeo);

        // polyline
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(points[0], false);
            for (int i = 1; i < points.Length; i++)
                ctx.LineTo(points[i]);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, linePen, geo);

        // cursor line at latest point
        var cursor = points[^1];
        context.DrawLine(brightLine, new Point(cursor.X, padding), new Point(cursor.X, padding + drawH));

        // dot at latest value
        context.DrawEllipse(new SolidColorBrush(fillColor), null, cursor, 3, 3);

        var bottomTypeface = new Typeface("Segoe UI", weight: FontWeight.Normal);
        var bottomText = new FormattedText("0%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bottomTypeface, 9, textFg);
        context.DrawText(bottomText, new Point(padding, h - padding - 14));

        var topText = new FormattedText($"{maxVal:F0}%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bottomTypeface, 9, textFg);
        context.DrawText(topText, new Point(padding + drawW - topText.Width, h - padding - 14));
    }
}
