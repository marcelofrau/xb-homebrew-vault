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
    public ErrorDialog()
    {
        InitializeComponent();
    }

    public ErrorDialog(string title, string description, string details, ErrorDialogType type) : this()
    {
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

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace("ErrorDialog closed by user");
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        Logger.Trace("ErrorDialog copy button clicked");
        var text = $"{TitleText.Text}\n\n{DescriptionText.Text}\n\n--- Details ---\n{DetailsText.Text}";
        try
        {
            if (Clipboard is { } cb)
                await cb.SetTextAsync(text);
        }
        catch { }
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
        catch { }

        Environment.Exit(1);
    }
}
