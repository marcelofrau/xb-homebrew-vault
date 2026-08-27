using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileErrorDialogView : UserControl
{
    public event EventHandler? OkClicked;

    public MobileErrorDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => LoadIcon();
    }

    private void LoadIcon()
    {
        if (DataContext is not MobileErrorDialogViewModel vm) return;
        var iconName = vm.DialogType switch
        {
            ErrorDialogType.Info  => "errordialog-info-48.png",
            ErrorDialogType.Warn  => "errordialog-warn-48.png",
            ErrorDialogType.Error => "errordialog-error-48.png",
            _                     => "errordialog-error-48.png"
        };
        try
        {
            var uri = new Uri($"avares://XBVault/Assets/Views/ErrorDialog/{iconName}");
            DialogIcon.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(uri));
        }
        catch { /* fallback — icon asset missing, dialog renders without icon */ }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        OkClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MobileErrorDialogViewModel vm) return;
        var url = vm.DownloadUrl;
        if (string.IsNullOrEmpty(url)) return;
        Logger.Info("MobileErrorDialog download button clicked");
        try
        {
            PlatformHelper.OpenUrl(url);
            OkClicked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MobileErrorDialog download failed");
        }
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MobileErrorDialogViewModel vm) return;
        var text = $"{vm.Title}\n\n{vm.Description}\n\n--- Details ---\n{vm.Details}";
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
            {
                var item = new DataTransferItem();
                item.Set(DataFormat.Text, text);
                var transfer = new DataTransfer();
                transfer.Add(item);
                cb.SetDataAsync(transfer).FireAndForget();
            }
        }
        catch (Exception ex)
        {
            Logger.Trace($"MobileErrorDialog: clipboard write failed — {ex.Message}");
        }
    }
}

public partial class MobileErrorDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Error";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string? _details;
    [ObservableProperty] private string? _downloadUrl;
    [ObservableProperty] private ErrorDialogType _dialogType = ErrorDialogType.Error;

    public bool HasDetails => !string.IsNullOrEmpty(Details);
    public bool HasDownload => !string.IsNullOrEmpty(DownloadUrl);
}
