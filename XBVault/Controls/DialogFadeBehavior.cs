using Avalonia;
using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using XBVault;
using XBVault.Services;

namespace XBVault.Controls;

public static class DialogFadeBehavior
{
    // Fade-out pause so the opacity drop is visible before the window closes
    private static readonly TimeSpan FadeOutDelay = TimeSpan.FromMilliseconds(200);

    public static readonly AttachedProperty<bool> EnableDialogFadeProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("EnableDialogFade", typeof(DialogFadeBehavior));

    static DialogFadeBehavior()
    {
        EnableDialogFadeProperty.Changed.AddClassHandler<Window>(OnEnableChanged);
    }

    public static void SetEnableDialogFade(Window window, bool value) =>
        window.SetValue(EnableDialogFadeProperty, value);

    public static bool GetEnableDialogFade(Window window) =>
        window.GetValue(EnableDialogFadeProperty);

    private static void OnEnableChanged(Window window, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            window.Opened += OnOpened;
            window.Closing += OnClosing;
            window.Closed += OnClosed;
        }
    }

    private static void OnOpened(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.Opened -= OnOpened;
        window.Opacity = 1;

        if (window.Owner is MainWindow main)
            main.IsModalDimmed = true;
    }

    private static async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not Window window) return;

        e.Cancel = true;
        window.Closing -= OnClosing;
        window.Opacity = 0;
        // run delay without capturing synchronization context
        await Task.Delay(FadeOutDelay).ConfigureAwait(false);

        ClearDim(window);
        window.Close();
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.Closed -= OnClosed;
        ClearDim(window);
    }

    private static void ClearDim(Window window)
    {
        if (window.Owner is MainWindow main)
        {
            Logger.Trace("[DialogFade] Clearing IsModalDimmed");
            main.IsModalDimmed = false;
        }
        else
        {
            Logger.Debug($"[DialogFade] Owner is {window.Owner?.GetType().Name ?? "null"}, cannot clear dim");
        }
    }
}
