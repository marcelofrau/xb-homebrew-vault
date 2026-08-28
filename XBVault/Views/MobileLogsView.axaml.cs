using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileLogsView : UserControl, IDisposable
{
    private static readonly FilePickerFileType[] SaveLogFileTypes =
    [
        new FilePickerFileType("ZIP Archive") { Patterns = new[] { "*.zip" } },
        new FilePickerFileType("Log file") { Patterns = new[] { "*.log" } },
        new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
    ];

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
            Dispatcher.UIThread.Post(ScrollToBottom, DispatcherPriority.Loaded);
    }

    private void ScrollToBottom()
    {
        // Scroll the last entry into view instead of computing Offsets off a stale Extent —
        // ScrollIntoView handles virtualization and measures the freshly-added item first.
        if (LogListBox.ItemCount > 0 && LogListBox.Items[LogListBox.ItemCount - 1] is { } lastItem)
            LogListBox.ScrollIntoView(lastItem);

        var sv = GetScrollViewer();
        if (sv is not null && sv.Extent.Height > 0)
            sv.Offset = new Vector(0, Math.Max(sv.Offset.Y, sv.Extent.Height - sv.Viewport.Height));
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

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null) return;

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture);
                var defaultName = $"xbvault-logs-{timestamp}.zip";

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Logs",
                    SuggestedFileName = defaultName,
                    FileTypeChoices = SaveLogFileTypes
                });

                if (file is null) return;

                var logDir = Logger.LogDirectory;
                if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                {
                    UploadStatusText.Text = "No logs found.";
                    UploadOverlay.IsVisible = true;
                    await Task.Delay(2000);
                    UploadOverlay.IsVisible = false;
                    return;
                }

                var localPath = file.TryGetLocalPath();
                var targetFile = await file.OpenWriteAsync();
                if (targetFile is null)
                {
                    UploadStatusText.Text = "Could not open destination file.";
                    UploadOverlay.IsVisible = true;
                    await Task.Delay(2000);
                    UploadOverlay.IsVisible = false;
                    return;
                }

                if (!string.IsNullOrEmpty(localPath) && localPath.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                {
                    var latestLog = Directory.GetFiles(logDir, "XBVault-*.log")
                        .OrderByDescending(f => f, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (latestLog is not null)
                    {
                        await using var src = File.OpenRead(latestLog);
                        await src.CopyToAsync(targetFile);
                    }
                }
                else
                {
                    var logFiles = Directory.GetFiles(logDir, "XBVault-*.log")
                        .OrderByDescending(f => f, StringComparer.Ordinal)
                        .ToArray();

                    using var archive = new ZipArchive(targetFile, ZipArchiveMode.Create, leaveOpen: true);
                    foreach (var logFile in logFiles)
                    {
                        archive.CreateEntryFromFile(logFile, Path.GetFileName(logFile));
                    }
                }
                await targetFile.DisposeAsync();

                UploadStatusText.Text = $"Saved to: {file.Name}";
                UploadOverlay.IsVisible = true;
                await Task.Delay(2000);
                UploadOverlay.IsVisible = false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SaveLogs");
                UploadStatusText.Text = $"Error: {ex.Message}";
                UploadOverlay.IsVisible = true;
                await Task.Delay(3000);
                UploadOverlay.IsVisible = false;
            }
        }

        Handler().FireAndForget("MobileLogsView.OnSaveClick");
    }

    private void OnShareClick(object? sender, RoutedEventArgs e)
    {
        async Task Handler()
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

        Handler().FireAndForget("MobileLogsView.OnShareClick");
    }

    public void Dispose()
    {
        _shareCts?.Cancel();
        _shareCts?.Dispose();
        _shareCts = null;
        GC.SuppressFinalize(this);
    }
}
