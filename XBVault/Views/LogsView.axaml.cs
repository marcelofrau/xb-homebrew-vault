#nullable enable
using System.Collections.Specialized;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class LogsView : UserControl
{
    private const int CopyFeedbackDelayMs = 2000;

    public LogsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel vm)
        {
            vm.Logs.CollectionChanged += OnLogsChanged;
            ScrollToBottom();
        }
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is LogsViewModel vm && vm.AutoScroll)
        {
            XBVault.Helpers.UIHelpers.RunOnUI(ScrollToBottom);
        }
    }

    private void ScrollToBottom()
    {
        LogScrollViewer?.ScrollToEnd();
    }

    private void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not LogsViewModel vm || vm.Logs.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var entry in vm.Logs)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(entry.ToString());
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            clipboard.SetTextAsync(sb.ToString()).FireAndForget();

        var orig = CopyButtonText.Text;
        CopyButtonText.Text = "Copied!";
        // restore text after delay without blocking UI thread
        Task.Run(async () =>
        {
            await Task.Delay(CopyFeedbackDelayMs).ConfigureAwait(false);
            // update UI on UI thread
            XBVault.Helpers.UIHelpers.RunOnUI(() => CopyButtonText.Text = orig);
        }).FireAndForget();
    }

    private void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel vm)
            vm.Logs.Clear();
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is LogsViewModel vm)
        {
            vm.CloseFilterCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnShareLogsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        async Task Handler()
        {
            ShareLogsButton.IsEnabled = false;
            ShareOverlay.IsVisible = true;
            ShareStatusText.Text = "Preparing logs...";
            try
            {
                var url = await XBVault.Services.LogShareService.ShareAllLogsAsync();
                if (string.IsNullOrEmpty(url))
                {
                    ShareStatusText.Text = "No logs to share.";
                    await Task.Delay(2000);
                    ShareOverlay.IsVisible = false;
                    return;
                }

                ShareStatusText.Text = "Generating QR code...";
                var qrBitmap = XBVault.Services.QRCodeService.GenerateQrBitmap(url);
                var window = new QrDialogWindow(url, qrBitmap);
                var parentWindow = TopLevel.GetTopLevel(this) as Window;
                if (parentWindow is not null)
                    await window.ShowDialog(parentWindow);
            }
            finally
            {
                ShareOverlay.IsVisible = false;
                ShareLogsButton.IsEnabled = true;
            }
        }

        Handler().FireAndForget("LogsView.OnShareLogsClick");
    }
}
