using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Diagnostics;

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
            ErrorDialogType.Info  => "app_info.ico",
            ErrorDialogType.Warn  => "app_warn.ico",
            ErrorDialogType.Error => "app_error.ico",
            _                     => "app_error.ico"
        };

        try
        {
            var uri = new Uri($"avares://XBVault/Assets/Icons/{iconName}");
            IconImage.Source = new Bitmap(AssetLoader.Open(uri));
        }
        catch
        {
            // icon not found — continue without
        }

        // Show restart button only for Error level
        RestartBtn.IsVisible = type == ErrorDialogType.Error;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
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
