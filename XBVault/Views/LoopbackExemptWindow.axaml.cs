#nullable enable
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class LoopbackExemptWindow : Window
{
    private DispatcherTimer? _spinTimer;
    private double _spinAngle;

    public LoopbackExemptWindow()
    {
        try
        {
            InitializeComponent();
            Logger.Info("LoopbackExemptWindow InitializeComponent OK");
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "LoopbackExemptWindow InitializeComponent FAILED");
            throw;
        }
        Loaded += (_, _) => StartSpin();
        Unloaded += (_, _) => StopSpin();
        KeyDown += OnKeyDown;
        Opened += (_, _) => WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is LoopbackExemptViewModel vm && vm.ShowCancel)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
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
        if (RunSpinner?.RenderTransform is RotateTransform rt)
            rt.Angle = _spinAngle;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
