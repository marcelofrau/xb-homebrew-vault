using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class UsbPermissionWindow : Window
{
    private DispatcherTimer? _spinTimer;
    private double _angle;

    public UsbPermissionWindow()
    {
        try
        {
            InitializeComponent();
            Loaded += (_, _) => StartSpin();
            Unloaded += (_, _) => StopSpin();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "UsbPermissionWindow init failed");
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
        _angle = (_angle - 6 + 360) % 360;
        if (ApplySpinner.RenderTransform is RotateTransform rt)
            rt.Angle = _angle;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is UsbPermissionViewModel vm)
            vm.CancelCommand.Execute(null);
        else
            Close();
    }

    private void OnDriveSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && DataContext is UsbPermissionViewModel vm && cb.SelectedIndex >= 0)
            vm.SelectedDriveIndex = cb.SelectedIndex;
    }
}
