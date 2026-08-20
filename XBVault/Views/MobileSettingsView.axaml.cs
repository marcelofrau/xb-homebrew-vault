using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileSettingsView : UserControl
{
    private Action? _onBack;

    public MobileSettingsView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
        SaveBtn.Click += OnSaveClick;
        TestConnBtn.Click += OnTestConnectionClick;
        AttachedToVisualTree += OnAttached;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Commented out — device handles insets automatically
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SaveSettingsCommand.Execute(null);
    }

    private void OnTestConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.TestConnectionCommand.Execute(null);
    }
}
