using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace XBVault.Controls;

public class CdSpinner : Grid
{
    private readonly RotateTransform _rotate = new();
    private DispatcherTimer? _timer;
    private double _angle;

    public CdSpinner()
    {
        RenderTransform = _rotate;
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
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
            [!HorizontalAlignmentProperty] = this[!HorizontalAlignmentProperty],
        };

        var loadingText = new TextBlock
        {
            Text = "Loading...",
            FontSize = 11,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = (IBrush?)Application.Current?.FindResource("TextDimBrush")
                         ?? new SolidColorBrush(Colors.Gray),
        };

        Children.Add(cd);
        Children.Add(loadingText);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_timer is null) return;
        _timer.Tick -= OnTick;
        _timer.Stop();
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _angle = (_angle - 6 + 360) % 360;
        _rotate.Angle = _angle;
    }
}
