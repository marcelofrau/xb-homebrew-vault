#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using XBVault.Helpers;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class SystemInfoWindow : Window
{
    private System.Threading.Timer? _copyTipTimer;

    public SystemInfoWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += (_, _) => _copyTipTimer?.Dispose();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is SystemInfoViewModel vm)
            vm.Initialize();
        if (Owner is { } owner)
        {
            var w = owner.Bounds.Width - 48;
            var h = owner.Bounds.Height - 96;
            MaxWidth = w;
            MaxHeight = h;
            if (Width > w) Width = w;
            if (Height > h) Height = h;

            // Re-center over the owner after the size clamp, mirroring how the
            // screenshot window stays aligned with the main window.
            if (owner is Window ownerWindow)
            {
                var origin = ownerWindow.Position;
                var x = origin.X + (int)((ownerWindow.Bounds.Width - Width) / 2);
                var y = origin.Y + (int)((ownerWindow.Bounds.Height - Height) / 2);
                Position = new PixelPoint(x, y);
            }
        }
        WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text } || string.IsNullOrEmpty(text))
            return;
        _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text);
        ShowCopiedFeedback();
    }

    private void ShowCopiedFeedback()
    {
        if (CopiedTip is null)
            return;
        CopiedTip.IsVisible = true;
        _copyTipTimer?.Dispose();
        _copyTipTimer = new System.Threading.Timer(_ =>
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => CopiedTip.IsVisible = false);
        }, null, 1500, System.Threading.Timeout.Infinite);
    }
}
