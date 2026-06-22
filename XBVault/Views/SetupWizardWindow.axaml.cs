using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
}
