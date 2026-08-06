using Avalonia.Controls;
using Avalonia.Input;
using XBVault.Helpers;
using XBVault.Services;

namespace XBVault.Views;

public partial class ProcessesWindow : Window
{
    public ProcessesWindow()
    {
        InitializeComponent();
        Opened += (_, _) => WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);
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
