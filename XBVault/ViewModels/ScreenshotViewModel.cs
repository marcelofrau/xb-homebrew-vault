using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class ScreenshotViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public ScreenshotViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
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

    public Func<Stream, Task<string?>>? SaveScreenshotDialog { get; set; }

    [RelayCommand]
    private async Task CaptureScreenshotAsync()
    {
        IsCapturing = true;
        StatusMessage = null;

        try
        {
            var data = await _xboxService.CaptureScreenshotAsync();
            if (data is null)
            {
                StatusMessage = "Screenshot not available — Xbox Dev Mode does not support this API";
                return;
            }

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
