using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;
using Avalonia.Input;

namespace XBVault.ViewModels;

public partial class ScreenshotViewModel : ObservableObject, IDisposable
{
    private readonly XboxDeviceService _xboxService;
    private CancellationTokenSource? _liveCts;

    public void Dispose()
    {
        _liveCts?.Cancel();
        _liveCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    public ScreenshotViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isCapturing;

    [ObservableProperty]
    private Bitmap? _screenshotImage;

    public bool HasScreenshot => ScreenshotImage is not null;

    partial void OnScreenshotImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasScreenshot));
    }

    [ObservableProperty]
    private string? _statusMessage;

    public List<string> IntervalOptions { get; } =
        ["0.8s", "1.0s", "1.5s", "2.0s", "2.5s", "3.0s", "3.5s", "4.0s", "4.5s", "5.0s"];

    [ObservableProperty]
    private string _selectedInterval = "1.0s";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveButtonText))]
    [NotifyPropertyChangedFor(nameof(Cursor))]
    private bool _isLiveCapturing;

    public Cursor? Cursor => (IsCapturing || IsLiveCapturing) ? AppStartingCursor : null;

    private static readonly Cursor AppStartingCursor = new(StandardCursorType.AppStarting);

    public string LiveButtonText => IsLiveCapturing ? "Stop Live" : "Start Live";

    public Func<Stream, Task<string?>>? SaveScreenshotDialog { get; set; }

    public void Cleanup()
    {
        _liveCts?.Cancel();
    }

    private async Task<byte[]?> CaptureAsync(CancellationToken ct)
    {
        return await _xboxService.CaptureScreenshotAsync(ct);
    }

    [RelayCommand]
    private async Task CaptureScreenshotAsync()
    {
        if (IsLiveCapturing) return;

        IsCapturing = true;
        StatusMessage = null;

        var data = await _xboxService.CaptureScreenshotAsync();
        if (data is null)
        {
            StatusMessage = "Screenshot not available — Xbox Dev Mode does not support this API";
            IsCapturing = false;
            return;
        }

        try
        {
            using var ms = new MemoryStream(data);
            var bitmap = new Bitmap(ms);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScreenshotImage?.Dispose();
                ScreenshotImage = bitmap;
            });
        }
        catch (Exception ex)
        {
            StatusMessage = "Screenshot failed";
            Logger.Error(ex, "CaptureScreenshot failed");
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    private void ToggleLiveCapture()
    {
        if (IsLiveCapturing)
        {
            _liveCts?.Cancel();
            return;
        }

        IsLiveCapturing = true;
        IsCapturing = false;
        StatusMessage = null;

        _liveCts?.Dispose();
        _liveCts = new CancellationTokenSource();

        _ = RunLiveCaptureAsync(_liveCts.Token);
    }

    private async Task RunLiveCaptureAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var msDelay = (int)(double.Parse(SelectedInterval.TrimEnd('s'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture) * 1000);
                await Task.Delay(msDelay, token);
                token.ThrowIfCancellationRequested();

                var data = await CaptureAsync(token);
                if (data is not null)
                {
                    try
                    {
                        using var ms = new MemoryStream(data);
                        var bitmap = new Bitmap(ms);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ScreenshotImage?.Dispose();
                            ScreenshotImage = bitmap;
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Live capture frame failed");
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLiveCapturing = false;
            });
        }
    }

    [RelayCommand]
    private async Task SaveScreenshotAsync()
    {
        if (ScreenshotImage is null) return;
        if (SaveScreenshotDialog is null) return;

        using var ms = new MemoryStream();
        ScreenshotImage.Save(ms);
        ms.Position = 0;

        var path = await SaveScreenshotDialog(ms);
        if (!string.IsNullOrWhiteSpace(path))
            StatusMessage = $"Saved to {path}";
    }
}
