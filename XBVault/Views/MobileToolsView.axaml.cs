using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XBVault.Views;

public partial class MobileToolsView : UserControl
{
    private Action? _onBack;

    public MobileToolsView()
    {
        InitializeComponent();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();
}
