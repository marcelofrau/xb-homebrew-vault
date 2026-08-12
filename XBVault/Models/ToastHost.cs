namespace XBVault.Models;

public sealed class ToastHost
{
    public ToastHost(NotificationItem item) => Item = item;

    public NotificationItem Item { get; }
}
