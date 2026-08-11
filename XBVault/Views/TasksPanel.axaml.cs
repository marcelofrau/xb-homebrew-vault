using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Models;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class TasksPanel : UserControl
{
    public TasksPanel()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskCenterViewModel vm && sender is Button { DataContext: BackgroundTask task })
            vm.Cancel(task);
    }
}
