using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.Views;

public partial class NotificationsPanel : UserControl
{
    public NotificationsPanel()
    {
        InitializeComponent();
    }

    public event Action? CloseRequested;

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotificationCenterService service) return;
        if (sender is not Button { DataContext: NotificationItem item }) return;
        service.Dismiss(item.Id);
    }
}
