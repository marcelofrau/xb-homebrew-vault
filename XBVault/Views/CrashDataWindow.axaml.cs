using Avalonia.Controls;
using Avalonia.Input;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class CrashDataWindow : Window
{
    public CrashDataWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is CrashDataViewModel vm)
            vm.Initialize();
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