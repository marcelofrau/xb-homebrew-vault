using Avalonia.Controls;
using Avalonia.Input;
using XBVault.Helpers;

namespace XBVault;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = BuildInfo.DisplayVersion;
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
