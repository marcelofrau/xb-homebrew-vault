#nullable enable
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XBVault.Helpers;
using XBVault.Services;

namespace XBVault.Views;

public partial class SetupWizardWindow : Window
{
    public SetupWizardWindow()
    {
        try
        {
            InitializeComponent();
            Logger.Info("SetupWizardWindow InitializeComponent OK");
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "SetupWizardWindow InitializeComponent FAILED");
            throw;
        }
        Opened += (_, _) => WindowFitHelper.ApplyScale(this, SettingsService.Current.UiScale);
        PortTextBox.KeyDown += OnPortKeyDown;
        PortTextBox.TextChanged += OnPortTextChanged;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnDevModeLinkClick(object? sender, RoutedEventArgs e)
    {
        Logger.Info("Opening Emulation Revival Dev Mode info");
        Process.Start(new ProcessStartInfo("https://emulationrevival.github.io") { UseShellExecute = true });
    }

    private static void OnPortKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        if (key is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4
            or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9
            or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4
            or Key.NumPad5 or Key.NumPad6 or Key.NumPad7 or Key.NumPad8 or Key.NumPad9
            or Key.Back or Key.Delete or Key.Left or Key.Right or Key.Tab
            or Key.Home or Key.End)
            return;
        if ((key is Key.A or Key.C or Key.V or Key.X) && (e.KeyModifiers & KeyModifiers.Control) != 0) return;
        e.Handled = true;
    }

    private static void OnPortTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var text = tb.Text ?? "";
        var cleaned = new string(text.Where(char.IsDigit).ToArray());
        if (text != cleaned)
        {
            tb.Text = cleaned;
            tb.CaretIndex = cleaned.Length;
        }
    }
}
