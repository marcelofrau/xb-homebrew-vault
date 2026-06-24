using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace XBVault.Views;

public partial class InputDialog : Window
{
    public string? Value { get; private set; }

    public InputDialog()
    {
        InitializeComponent();
    }

    public InputDialog(string title, string message, string defaultValue, string? iconUri) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        InputTextBox.Text = defaultValue;

        if (!string.IsNullOrEmpty(iconUri))
        {
            DialogIcon.Source = new Bitmap(AssetLoader.Open(new Uri(iconUri)));
            DialogIcon.IsVisible = true;
        }

        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Value = InputTextBox.Text;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Value = InputTextBox.Text;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
