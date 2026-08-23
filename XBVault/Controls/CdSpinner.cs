#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Collections.Generic;

namespace XBVault.Controls;

public class CdSpinner : Grid
{
    private static readonly List<CdSpinner> _activeSpinners = [];
    private static readonly DispatcherTimer _sharedTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(12)
    };

    private readonly RotateTransform _rotate = new();
    private readonly TextBlock _loadingText;
    private double _angle;

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<CdSpinner, string>(nameof(StatusText), "Loading...");

    public static readonly StyledProperty<bool> ShowTextProperty =
        AvaloniaProperty.Register<CdSpinner, bool>(nameof(ShowText), true);

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public bool ShowText
    {
        get => GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    public CdSpinner()
    {
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        RowDefinitions = new RowDefinitions("Auto,Auto");

        Bitmap cdBitmap;
        using (var stream = AssetLoader.Open(new Uri("avares://XBVault/Assets/Views/BrowseView/browse-cdloading-100.png")))
            cdBitmap = new Bitmap(stream);

        var cd = new Image
        {
            Source = cdBitmap,
            Width = 64,
            Height = 64,
            Stretch = Stretch.Uniform,
            RenderTransform = _rotate,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        };

        _loadingText = new TextBlock
        {
            Text = "Loading...",
            FontSize = 11,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = (IBrush?)Application.Current?.FindResource("TextDimBrush")
                         ?? new SolidColorBrush(Colors.Gray),
        };

        Grid.SetRow(cd, 0);
        Grid.SetRow(_loadingText, 1);
        Children.Add(cd);
        Children.Add(_loadingText);

        PropertyChanged += (_, e) =>
        {
            if (e.Property == StatusTextProperty)
                _loadingText.Text = StatusText;
            if (e.Property == ShowTextProperty)
                _loadingText.IsVisible = ShowText;
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _activeSpinners.Add(this);
        if (_activeSpinners.Count == 1)
            _sharedTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _activeSpinners.Remove(this);
        if (_activeSpinners.Count == 0)
            _sharedTimer.Stop();
    }

    private static void OnSharedTick(object? sender, EventArgs e)
    {
        for (var i = 0; i < _activeSpinners.Count; i++)
        {
            var spinner = _activeSpinners[i];
            spinner._angle = (spinner._angle - 6 + 360) % 360;
            spinner._rotate.Angle = spinner._angle;
        }
    }

    static CdSpinner()
    {
        _sharedTimer.Tick += OnSharedTick;
    }
}
