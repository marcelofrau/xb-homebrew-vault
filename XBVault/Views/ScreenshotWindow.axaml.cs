using Avalonia.Controls;
using Avalonia.Input;

namespace XBVault.Views;

public partial class ScreenshotWindow : Window
{
    public ScreenshotWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is ViewModels.ScreenshotViewModel vm)
            vm.Cleanup();
        base.OnClosing(e);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
