using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using XBVault.Services;

namespace XBVault.Views;

public partial class ItemDetailWindow : Window
{
    private DispatcherTimer? _spinTimer;
    private double _spinAngle;

    public ItemDetailWindow()
    {
        try
        {
            InitializeComponent();
            Logger.Info("ItemDetailWindow InitializeComponent OK");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "ItemDetailWindow InitializeComponent FAILED");
            throw;
        }
        Loaded += (_, _) => {
            Logger.Info("ItemDetailWindow Loaded");
            StartSpin();
        };
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
        _spinAngle = (_spinAngle - 6 + 360) % 360;
        if (InstallSpinner?.RenderTransform is RotateTransform rt)
            rt.Angle = _spinAngle;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
