using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileQrDialogView : UserControl
{
    private string _url = "";
    private Action? _onBack;

    public MobileQrDialogView()
    {
        InitializeComponent();
    }

    public MobileQrDialogView(string url, Bitmap? qrBitmap) : this()
    {
        _url = url;
        UrlText.Text = url;
        QrImage.Source = qrBitmap;
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _onBack?.Invoke();
            e.Handled = true;
        }
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
            {
                var item = new DataTransferItem();
                item.Set(DataFormat.Text, _url);
                var transfer = new DataTransfer();
                transfer.Add(item);
                cb.SetDataAsync(transfer).FireAndForget();
            }
        }
        catch (Exception ex) { Logger.Warn($"QR dialog clipboard write failed: {ex.Message}"); }
        CopyButtonText.Text = "Copied!";
        Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() => CopyButtonText.Text = "Copy Link");
        });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _onBack?.Invoke();
    }
}
