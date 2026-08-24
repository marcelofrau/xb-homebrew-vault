using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileLogsView : UserControl, IDisposable
{
    private CancellationTokenSource? _shareCts;
    private Action? _onBack;

    public MobileLogsView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private ScrollViewer? GetScrollViewer()
    {
        return LogScrollViewer;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Logger.Entries.CollectionChanged += OnLogEntriesChanged;
        if (LogListBox.ItemCount > 0)
            ScrollToBottom();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Logger.Entries.CollectionChanged -= OnLogEntriesChanged;
        _shareCts?.Cancel();
        _shareCts?.Dispose();
        _shareCts = null;
    }

    private void OnLogEntriesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.LogsViewModel vm && vm.AutoScroll)
            Dispatcher.UIThread.Post(ScrollToBottom);
    }

    private void ScrollToBottom()
    {
        var sv = GetScrollViewer();
        if (sv is not null && sv.Extent.Height > 0)
            sv.Offset = new Vector(0, sv.Extent.Height - sv.Viewport.Height);
    }

    private static MobileMainWindow? WalkToMainWindow(Avalonia.Visual child)
    {
        var current = child as Avalonia.Visual;
        while (current is not null)
        {
            if (current is MobileMainWindow mwm)
                return mwm;
            current = current.Parent as Avalonia.Visual;
        }
        return null;
    }

    private async void OnShareClick(object? sender, RoutedEventArgs e)
    {
        if (_shareCts is not null) return;

        _shareCts = new CancellationTokenSource();
        var ct = _shareCts.Token;

        try
        {
            UploadOverlay.IsVisible = true;
            UploadStatusText.Text = "Collecting logs...";

            var progress = new Progress<double>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UploadStatusText.Text = p switch
                    {
                        < 0.3 => "Preparing logs...",
                        < 0.5 => "Zipping logs...",
                        < 0.9 => "Uploading to GoFile...",
                        _ => "Done!"
                    };
                });
            });

            var url = await LogShareService.ShareAllLogsAsync(progress, ct);

            UploadOverlay.IsVisible = false;

            if (url is null)
            {
                UploadStatusText.Text = "No logs to share.";
                UploadOverlay.IsVisible = true;
                await Task.Delay(2000);
                UploadOverlay.IsVisible = false;
                return;
            }

            // Show QR dialog
            var qrBitmap = QRCodeService.GenerateQrBitmap(url);
            var main = WalkToMainWindow(this);
            if (main is not null)
            {
                var qrView = new MobileQrDialogView(url, qrBitmap);
                qrView.SetOnBack(() => main.CloseOverlay());
                main.ShowOverlay(qrView);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Error(ex, "ShareLogs");
            UploadStatusText.Text = $"Error: {ex.Message}";
            UploadOverlay.IsVisible = true;
            await Task.Delay(3000);
            UploadOverlay.IsVisible = false;
        }
        finally
        {
            _shareCts?.Dispose();
            _shareCts = null;
        }
    }

    public void Dispose()
    {
        _shareCts?.Cancel();
        _shareCts?.Dispose();
        _shareCts = null;
        GC.SuppressFinalize(this);
    }
}
