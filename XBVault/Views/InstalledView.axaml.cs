using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace XBVault.Views;

public partial class InstalledView : UserControl
{
    private DispatcherTimer? _spinTimer;
    private double _angle;

    public InstalledView()
    {
        InitializeComponent();
        Loaded += (_, _) => StartSpin();
        Unloaded += (_, _) => StopSpin();
    }

    private void StartSpin()
    {
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += SpinTick;
        _spinTimer.Start();
    }

    private void StopSpin()
    {
        if (_spinTimer is null) return;
        _spinTimer.Tick -= SpinTick;
        _spinTimer.Stop();
        _spinTimer = null;
    }

    private void SpinTick(object? sender, EventArgs e)
    {
        _angle = (_angle - 6 + 360) % 360;
        if (SpinnerImage.RenderTransform is RotateTransform rt)
            rt.Angle = _angle;
    }
}
