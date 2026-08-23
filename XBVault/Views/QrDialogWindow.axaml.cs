using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using XBVault.Services;

namespace XBVault.Views;

public partial class QrDialogWindow : Window
{
    private string _url = "";

    public QrDialogWindow()
    {
        InitializeComponent();
    }

    public QrDialogWindow(string url, Bitmap? qrBitmap) : this()
    {
        _url = url;
        UrlText.Text = url;
        QrImage.Source = qrBitmap;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard is { } cb)
            {
                var item = new DataTransferItem();
                item.Set(DataFormat.Text, _url);
                var transfer = new DataTransfer();
                transfer.Add(item);
                cb.SetDataAsync(transfer).FireAndForget();
            }
        }
        catch { }
        CopyButtonText.Text = "Copied!";
        Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() => CopyButtonText.Text = "Copy");
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }
}
