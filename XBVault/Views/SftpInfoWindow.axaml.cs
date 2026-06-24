using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace XBVault.Views;

public partial class SftpInfoWindow : Window
{
    public SftpInfoWindow()
    {
        InitializeComponent();
    }

    public void SetConnectionInfo(string host, string user, string password, int port)
    {
        HostText.Text = host;
        PortText.Text = port.ToString();
        UserText.Text = user;
        PasswordText.Text = password;
    }

    private async void OnCopyHostClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } cb)
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, HostText.Text ?? "");
            var transfer = new DataTransfer();
            transfer.Add(item);
            await cb.SetDataAsync(transfer);
        }
    }

    private async void OnCopyPortClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } cb)
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, PortText.Text ?? "");
            var transfer = new DataTransfer();
            transfer.Add(item);
            await cb.SetDataAsync(transfer);
        }
    }

    private async void OnCopyUserClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } cb)
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, UserText.Text ?? "");
            var transfer = new DataTransfer();
            transfer.Add(item);
            await cb.SetDataAsync(transfer);
        }
    }

    private async void OnCopyPasswordClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } cb)
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, PasswordText.Text ?? "");
            var transfer = new DataTransfer();
            transfer.Add(item);
            await cb.SetDataAsync(transfer);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
