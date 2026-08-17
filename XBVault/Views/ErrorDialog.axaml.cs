using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Diagnostics;
using XBVault.Services;

namespace XBVault.Views;

public enum ErrorDialogType { Info, Warn, Error }

public partial class ErrorDialog : Window
{
    private Func<Task>? _connectAction;
    public Func<Task>? ConnectAction
    {
        get => _connectAction;
        set
        {
            _connectAction = value;
            ConnectBtn.IsVisible = value is not null;
        }
    }

    private Func<Task>? _downloadAction;
    public Func<Task>? DownloadAction
    {
        get => _downloadAction;
        set
        {
            _downloadAction = value;
            DownloadBtn.IsVisible = value is not null;
        }
    }

    public ErrorDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    public ErrorDialog(string title, string description, string details, ErrorDialogType type) : this()
    {
        Title = $"XBVault - {title}";
        TitleText.Text = title;
        DescriptionText.Text = description;
        DetailsText.Text = details;

        var iconName = type switch
        {
            ErrorDialogType.Info  => "errordialog-info-48.png",
            ErrorDialogType.Warn  => "errordialog-warn-48.png",
            ErrorDialogType.Error => "errordialog-error-48.png",
            _                     => "errordialog-error-48.png"
        };

        try
        {
            var uri = new Uri($"avares://XBVault/Assets/Views/ErrorDialog/{iconName}");
            IconImage.Source = new Bitmap(AssetLoader.Open(uri));
        }
        catch
        {
        }

        RestartBtn.IsVisible = type == ErrorDialogType.Error;
        Logger.Debug($"ErrorDialog shown: type={type} title='{title}'");
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ConnectBtn.IsVisible)
            ConnectBtn.Focus();
        else
            CloseBtn.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace("ErrorDialog closed by user");
        Close();
    }

    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (ConnectAction is null) return;
        Logger.Trace("ErrorDialog connect button clicked");
        // run action without blocking UI thread and capture exceptions
        Task.Run(async () =>
        {
            try
            {
                await ConnectAction().ConfigureAwait(false);
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ErrorDialog connect action failed");
            }
        }).FireAndForget();
    }

    private void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        if (DownloadAction is null) return;
        Logger.Info("ErrorDialog download button clicked");
        Task.Run(async () =>
        {
            try
            {
                await DownloadAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ErrorDialog download action failed");
            }
        }).FireAndForget();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace("ErrorDialog copy button clicked");
        var text = $"{TitleText.Text}\n\n{DescriptionText.Text}\n\n--- Details ---\n{DetailsText.Text}";
        try
        {
            if (Clipboard is { } cb)
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
            // Clipboard write refused (platform policy) — copy is best-effort, never crash the error dialog
            Logger.Trace($"ErrorDialog: clipboard write failed — {ex.Message}");
        }
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        Logger.Info("ErrorDialog restart clicked — launching new process");
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is not null)
                Process.Start(exe);
        }
        catch (Exception ex)
        {
            // Process.Start can fail (blocked exe, sandbox) — log and fall through to the hard exit
            Logger.Trace($"ErrorDialog: restart launch failed — {ex.Message}");
        }

        Environment.Exit(1);
    }
}
