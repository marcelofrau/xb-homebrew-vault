using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class PerformanceWindow : Window
{
    public PerformanceWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is PerformanceViewModel vm)
            vm.StopMonitoringCommand.Execute(null);
        Close();
    }
}
