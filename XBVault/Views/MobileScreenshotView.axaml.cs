using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileScreenshotView : UserControl
{
    private Action? _onBack;

    public MobileScreenshotView()
    {
        InitializeComponent();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;
}

public partial class MobileScreenshotViewModel : ObservableObject, IDisposable
{
    private readonly IXboxSystemService _systemService;

    public MobileScreenshotViewModel(IXboxSystemService systemService)
    {
        _systemService = systemService;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private Bitmap? _screenshotImage;

    public bool HasScreenshot => ScreenshotImage is not null;
    public bool ShowCaptureButton => !IsCapturing && !HasScreenshot;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public Func<Stream, Task<string?>>? SaveScreenshotDialog { get; set; }

    partial void OnScreenshotImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasScreenshot));
        OnPropertyChanged(nameof(ShowCaptureButton));
    }

    [RelayCommand]
    private async Task CaptureScreenshotAsync()
    {
        if (IsCapturing) return;
        IsCapturing = true;
        StatusMessage = null;

        try
        {
            var data = await _systemService.CaptureScreenshotAsync();
            if (data is null)
            {
                StatusMessage = "Screenshot not available — Xbox Dev Mode may not support this API";
                IsCapturing = false;
                return;
            }

            using var ms = new MemoryStream(data);
            var bitmap = new Bitmap(ms);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScreenshotImage?.Dispose();
                ScreenshotImage = bitmap;
            });
        }
        catch (Exception ex)
        {
            StatusMessage = "Screenshot failed";
            Services.Logger.Error(ex, "Mobile screenshot failed");
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    private async Task SaveScreenshotAsync()
    {
        if (ScreenshotImage is null || SaveScreenshotDialog is null) return;

        using var ms = new MemoryStream();
        ScreenshotImage.Save(ms);
        ms.Position = 0;

        var path = await SaveScreenshotDialog(ms);
        if (!string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = $"Saved to {path}";
            OnPropertyChanged(nameof(HasStatus));
        }
    }
}
