using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileSettingsView : UserControl
{
    private Action? _onBack;
    private static IBrush FindBrush(string name) => (IBrush)Application.Current!.FindResource(name)!;

    public MobileSettingsView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
        SaveBtn.Click += OnSaveClick;
        TestConnBtn.Click += OnTestConnectionClick;
        AttachedToVisualTree += OnAttached;

        PortBox.KeyDown += OnPortKeyDown;
        PortBox.TextChanged += (_, _) => ValidatePort();
        PortBox.TextChanged += OnPortTextChanged;
        AddressBox.TextChanged += (_, _) => ValidateAddress();
        UsernameBox.TextChanged += (_, _) => ValidateUsername();
        PasswordBox.TextChanged += (_, _) => ValidatePassword();
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    private void OnBackClick(object? sender, RoutedEventArgs e) => _onBack?.Invoke();

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!AreFieldsValid()) return;
        if (DataContext is SettingsViewModel vm)
            vm.SaveSettingsCommand.Execute(null);
    }

    private void OnTestConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (!AreFieldsValid()) return;
        if (DataContext is SettingsViewModel vm)
            vm.TestConnectionCommand.Execute(null);
    }

    // ── Field validation ──────────────────────────────────────────────
    private void ValidateAddress()
    {
        var error = XBVault.Helpers.NetworkValidationHelper.ValidateAddress(AddressBox.Text);
        SetFieldInvalid(AddressBox, AddressError, error);
    }

    private void ValidatePort()
    {
        var text = PortBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            SetFieldInvalid(PortBox, PortError, "Port is required");
            return;
        }
        if (!int.TryParse(text, out var portVal) || portVal < 1 || portVal > 65535)
        {
            SetFieldInvalid(PortBox, PortError, "Must be 1-65535");
            return;
        }
        SetFieldInvalid(PortBox, PortError, "");
    }

    private void ValidateUsername()
    {
        var valid = !string.IsNullOrWhiteSpace(UsernameBox.Text);
        SetFieldInvalid(UsernameBox, UsernameError, valid ? "" : "Username is required");
    }

    private void ValidatePassword()
    {
        var valid = !string.IsNullOrWhiteSpace(PasswordBox.Text);
        SetFieldInvalid(PasswordBox, PasswordError, valid ? "" : "Password is required");
    }

    private bool AreFieldsValid()
    {
        ValidateAddress();
        ValidatePort();
        ValidateUsername();
        ValidatePassword();
        return AddressError.Text == "" && PortError.Text == ""
            && UsernameError.Text == "" && PasswordError.Text == "";
    }

    private static void SetFieldInvalid(TextBox box, TextBlock errorBlock, string error)
    {
        var hasError = !string.IsNullOrEmpty(error);
        errorBlock.Text = error;
        errorBlock.IsVisible = hasError;
        box.BorderBrush = hasError
            ? new SolidColorBrush(Color.Parse("#E74C3C"))
            : FindBrush("AccentBrush");
    }

    private static void OnPortKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox) return;
        var key = e.Key;
        if (key is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4
            or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9
            or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4
            or Key.NumPad5 or Key.NumPad6 or Key.NumPad7 or Key.NumPad8 or Key.NumPad9
            or Key.Back or Key.Delete or Key.Left or Key.Right or Key.Tab
            or Key.Home or Key.End)
            return;
        if (key is Key.A or Key.C or Key.V or Key.X && (e.KeyModifiers & KeyModifiers.Control) != 0) return;
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
