using Avalonia;
using Avalonia.Controls;
using System;
using System.Threading.Tasks;

namespace XBVault.Controls;

public static class DialogFadeBehavior
{
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
        }
    }

    private static void OnOpened(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.Opened -= OnOpened;
        window.Opacity = 1;
    }

    private static async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not Window window) return;

        e.Cancel = true;
        window.Closing -= OnClosing;
        window.Opacity = 0;
        await Task.Delay(200);
        window.Close();
    }
}
