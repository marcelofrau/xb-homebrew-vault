#nullable enable
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotificationCenterService service) return;
        if (sender is not Button { DataContext: NotificationAction action } btn) return;

        try
        {
            action.Action?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "NotificationsPanel: notification action threw");
        }

        var item = btn.FindAncestorOfType<Border>()?.DataContext as NotificationItem;
        if (item is not null)
            service.Dismiss(item.Id);
    }

    private void OnHistoryClearClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotificationCenterService service) return;
        if (sender is not Button { DataContext: NotificationItem item }) return;
        service.RemoveFromHistory(item.Id);
    }

    private void OnClearAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NotificationCenterService service)
            service.ClearAll();
    }
}
