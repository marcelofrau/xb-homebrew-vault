using Avalonia.Controls;
using Avalonia.Input;

namespace XBVault.Views;

public partial class DeleteConfirmWindow : Window
{
    public DeleteConfirmWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ViewModels.DeleteConfirmViewModel vm)
        {
            vm.Completed += _ => Close();
        }
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
