using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XBVault.Views;

public partial class MobileSettingsView : UserControl
{
    private Action? _onBack;

    public MobileSettingsView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Commented out — device handles insets automatically
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();
}
