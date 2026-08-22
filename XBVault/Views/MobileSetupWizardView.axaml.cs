using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileSetupWizardView : UserControl
{
    private SetupWizardViewModel? _vm;
    private readonly TextBox _addressBox;
    private readonly TextBox _portBox;
    private readonly CheckBox _httpsCheck;
    private readonly TextBox _usernameBox;
    private readonly TextBox _passwordBox;
    private TextBlock _addressError = null!;
    private TextBlock _portError = null!;
    private TextBlock _usernameError = null!;
    private TextBlock _passwordError = null!;

    private readonly StackPanel _step0Content;
    private readonly StackPanel _step1Content;
    private readonly StackPanel _step2Content;
    private readonly StackPanel _step3Content;
    private TextBlock _sumAddress = null!;
    private TextBlock _sumPort = null!;
    private TextBlock _sumHttps = null!;
    private TextBlock _sumUsername = null!;

    private static readonly FontFamily TitleFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-700.ttf#Oxanium");
    private static readonly FontFamily BodyFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-400.ttf#Oxanium");
    private static readonly Uri AssetsBase = new("avares://XBVault/Assets/Views/SetupWizardWindow/");

    private static IBrush FindBrush(string name) => (IBrush)Application.Current!.FindResource(name)!;

    public MobileSetupWizardView()
    {
        InitializeComponent();

        _addressBox = MakeTextBox("e.g. 192.168.1.100");
        _addressBox.TextChanged += (_, _) => UpdateNextEnabled();
        _addressBox.TextChanged += (_, _) => ValidateAddress();

        _portBox = MakeTextBox("11443");
        _portBox.TextChanged += (_, _) => UpdateNextEnabled();
        _portBox.TextChanged += (_, _) => ValidatePort();
        _portBox.TextChanged += OnPortTextChanged;
        _portBox.KeyDown += OnPortKeyDown;

        _httpsCheck = new CheckBox
        {
            Content = "Use HTTPS (recommended)",
            FontSize = 14,
            FontFamily = BodyFont,
            Foreground = FindBrush("TextBrush"),
            IsChecked = true,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _usernameBox = MakeTextBox("e.g. DevPortalUser");
        _usernameBox.TextChanged += (_, _) => UpdateNextEnabled();
        _usernameBox.TextChanged += (_, _) => ValidateUsername();

        _passwordBox = new TextBox
        {
            PlaceholderText = "Password",
            FontSize = 15,
            FontFamily = BodyFont,
            PasswordChar = '*',
            Margin = new Thickness(0, 4, 0, 0)
        };
        _passwordBox.TextChanged += (_, _) => UpdateNextEnabled();
        _passwordBox.TextChanged += (_, _) => ValidatePassword();

        _step0Content = BuildStep0();
        _step1Content = BuildStep1();
        _step2Content = BuildStep2();
        _step3Content = BuildStep3();
    }

    public void SetViewModel(SetupWizardViewModel vm)
    {
        _vm = vm;

        Wizard.BackRequested += OnWizardBack;
        Wizard.CancelRequested += OnWizardCancel;
        Wizard.StepChanged += OnWizardStepChanged;
        Wizard.FinishClicked += OnWizardFinish;

        _addressBox.Text = vm.Address ?? "";
        _portBox.Text = vm.Port ?? "11443";
        _httpsCheck.IsChecked = vm.UseHttps;
        _usernameBox.Text = vm.Username ?? "";
        _passwordBox.Text = vm.Password ?? "";

        Wizard.InitSteps("Setup Wizard", ["Welcome", "Console", "Auth", "Ready"]);
        Wizard.SetWizardTitle("Setup Wizard");

        _httpsCheck.PropertyChanged += (_, _) => UpdateNextEnabled();

        NavigateToStep(0);
    }

    // ── Field validation ──────────────────────────────────────────────
    private void ValidateAddress()
    {
        var error = XBVault.Helpers.NetworkValidationHelper.ValidateAddress(_addressBox.Text);
        SetFieldInvalid(_addressBox, _addressError, error);
    }

    private void ValidatePort()
    {
        var text = _portBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            SetFieldInvalid(_portBox, _portError, "Port is required");
            return;
        }
        if (!int.TryParse(text, out var portVal) || portVal < 1 || portVal > 65535)
        {
            SetFieldInvalid(_portBox, _portError, "Must be 1-65535");
            return;
        }
        SetFieldInvalid(_portBox, _portError, "");
    }

    private void ValidateUsername()
    {
        var valid = !string.IsNullOrWhiteSpace(_usernameBox.Text);
        SetFieldInvalid(_usernameBox, _usernameError, valid ? "" : "Username is required");
    }

    private void ValidatePassword()
    {
        var valid = !string.IsNullOrWhiteSpace(_passwordBox.Text);
        SetFieldInvalid(_passwordBox, _passwordError, valid ? "" : "Password is required");
    }

    private static void SetFieldInvalid(TextBox box, TextBlock errorBlock, string error)
    {
        var hasError = !string.IsNullOrEmpty(error);
        errorBlock.Text = error;
        errorBlock.IsVisible = hasError;
        box.BorderBrush = hasError
            ? new SolidColorBrush(Color.Parse("#E74C3C"))
            : (IBrush)Application.Current!.FindResource("AccentBrush")!;
    }

    private static void OnPortKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var key = e.Key;
        if (key is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4
            or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9
            or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4
            or Key.NumPad5 or Key.NumPad6 or Key.NumPad7 or Key.NumPad8 or Key.NumPad9
            or Key.Back or Key.Delete or Key.Left or Key.Right or Key.Tab
            or Key.Home or Key.End)
            return;

        if (key is Key.A && (e.KeyModifiers & KeyModifiers.Control) != 0) return;
        if (key is Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0) return;
        if (key is Key.V && (e.KeyModifiers & KeyModifiers.Control) != 0) return;
        if (key is Key.X && (e.KeyModifiers & KeyModifiers.Control) != 0) return;

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

    private bool AreCurrentFieldsValid()
    {
        var step = _vm?.CurrentStep ?? 0;
        if (step == 1)
            return NetworkValidationHelper.ValidateAddress(_addressBox.Text) == string.Empty
                && int.TryParse(_portBox.Text, out var p) && p >= 1 && p <= 65535;
        if (step == 2)
            return !string.IsNullOrWhiteSpace(_usernameBox.Text)
                && !string.IsNullOrWhiteSpace(_passwordBox.Text);
        return true;
    }

    // ── Build step content once ──────────────────────────────────────
    private StackPanel BuildStep0()
    {
        var content = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        content.Children.Add(new Image
        {
            Source = LoadImage("setupwizard-welcome-48.png"),
            Width = 72, Height = 72,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = "Welcome to XB Homebrew Vault",
            FontFamily = TitleFont,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });

        var descCard = MakeCard();
        var descStack = new StackPanel { Spacing = 8 };
        descStack.Children.Add(MakeRichText(
            "This wizard will help you connect to your Xbox in ",
            ("Developer Mode", true),
            ". Make sure your Xbox is powered on and in Developer Mode before continuing."));
        descCard.Child = descStack;
        content.Children.Add(descCard);

        var learnMoreBtn = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0),
            Content = new TextBlock
            {
                Text = "Learn more about Xbox Developer Mode",
                FontFamily = BodyFont,
                FontSize = 13,
                Foreground = FindBrush("AccentBrush"),
                TextDecorations = TextDecorations.Underline
            }
        };
        learnMoreBtn.Click += OnDevModeLinkClick;
        content.Children.Add(learnMoreBtn);

        content.Children.Add(MakeSeparator());

        content.Children.Add(new TextBlock
        {
            Text = "Tap Next to begin.",
            FontFamily = BodyFont,
            FontSize = 13,
            Foreground = FindBrush("TextDimBrush"),
            FontStyle = FontStyle.Italic,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return content;
    }

    private StackPanel BuildStep1()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        var card = MakeCard();
        var cardContent = new StackPanel { Spacing = 12 };
        cardContent.Children.Add(MakeFieldLabel("IP Address"));
        cardContent.Children.Add(_addressBox);
        _addressError = MakeErrorText();
        cardContent.Children.Add(_addressError);
        cardContent.Children.Add(MakeFieldLabel("Port"));
        cardContent.Children.Add(_portBox);
        _portError = MakeErrorText();
        cardContent.Children.Add(_portError);
        cardContent.Children.Add(_httpsCheck);
        card.Child = cardContent;
        content.Children.Add(card);

        var tipCard = MakeCard();
        tipCard.Background = new SolidColorBrush(Color.FromArgb(30, 0x22, 0x9A, 0x3C));
        tipCard.Child = new TextBlock
        {
            Text = "Find your Xbox IP in Settings > Developer Mode > Network. Both devices must be on the same network.",
            FontFamily = BodyFont,
            FontSize = 12,
            Foreground = FindBrush("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(tipCard);

        return content;
    }

    private StackPanel BuildStep2()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        var card = MakeCard();
        var cardContent = new StackPanel { Spacing = 12 };
        cardContent.Children.Add(MakeFieldLabel("Username"));
        cardContent.Children.Add(_usernameBox);
        _usernameError = MakeErrorText();
        cardContent.Children.Add(_usernameError);
        cardContent.Children.Add(MakeFieldLabel("Password"));
        cardContent.Children.Add(_passwordBox);
        _passwordError = MakeErrorText();
        cardContent.Children.Add(_passwordError);
        card.Child = cardContent;
        content.Children.Add(card);

        var tipCard = MakeCard();
        tipCard.Background = new SolidColorBrush(Color.FromArgb(30, 0x22, 0x9A, 0x3C));
        tipCard.Child = new TextBlock
        {
            Text = "These are the credentials you use to access Xbox Device Portal in your browser.",
            FontFamily = BodyFont,
            FontSize = 12,
            Foreground = FindBrush("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(tipCard);

        return content;
    }

    private StackPanel BuildStep3()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        var card = MakeCard();
        var cardStack = new StackPanel { Spacing = 10 };
        (_sumAddress, var row0) = MakeSummaryRow("Address");
        cardStack.Children.Add(row0);
        (_sumPort, var row1) = MakeSummaryRow("Port");
        cardStack.Children.Add(row1);
        (_sumHttps, var row2) = MakeSummaryRow("HTTPS");
        cardStack.Children.Add(row2);
        (_sumUsername, var row3) = MakeSummaryRow("Username");
        cardStack.Children.Add(row3);
        card.Child = cardStack;
        content.Children.Add(card);

        var openCheck = new CheckBox
        {
            Content = "Open connection window after setup",
            FontSize = 13,
            FontFamily = BodyFont,
            Foreground = FindBrush("TextBrush"),
            IsChecked = true,
            Margin = new Thickness(0, 4, 0, 0)
        };
        openCheck.PropertyChanged += (_, _) => { if (_vm is not null) _vm.OpenConnectionAfter = openCheck.IsChecked ?? true; };
        content.Children.Add(openCheck);

        return content;
    }

    // ── Navigate ─────────────────────────────────────────────────────
    private void NavigateToStep(int step)
    {
        if (_vm is null) return;
        _vm.CurrentStep = step;
        SyncToVm();

        switch (step)
        {
            case 0:
                Wizard.SetStepHero("setupwizard-wizard-100.png", "Welcome", "Xbox Developer Mode Setup");
                Wizard.SetStepContent(0, _step0Content);
                Wizard.SetFinishMode(false);
                Wizard.SetNextButtonEnabled(true);
                break;
            case 1:
                Wizard.SetStepHero("setupwizard-wizard-100.png", "Xbox Console",
                    "Enter the IP address of your Xbox in Developer Mode");
                Wizard.SetStepContent(1, _step1Content);
                Wizard.SetFinishMode(false);
                UpdateNextEnabled();
                break;
            case 2:
                Wizard.SetStepHero("setupwizard-wizard-100.png", "Authentication",
                    "Enter the credentials for your Xbox Device Portal");
                Wizard.SetStepContent(2, _step2Content);
                Wizard.SetFinishMode(false);
                UpdateNextEnabled();
                break;
            case 3:
                _sumAddress.Text = _addressBox.Text ?? "";
                _sumPort.Text = _portBox.Text ?? "";
                _sumHttps.Text = _httpsCheck.IsChecked == true ? "Yes" : "No";
                _sumUsername.Text = _usernameBox.Text ?? "";
                Wizard.SetStepHero("setupwizard-wizard-100.png", "Ready to Connect",
                    "Review your settings before saving");
                Wizard.SetStepContent(3, _step3Content);
                Wizard.SetFinishMode(true, "Finish");
                Wizard.SetNextButtonEnabled(true);
                break;
        }
    }

    // ── Navigation handlers ──────────────────────────────────────────
    private void SyncToVm()
    {
        if (_vm is null) return;
        _vm.Address = _addressBox.Text;
        _vm.Port = _portBox.Text;
        _vm.UseHttps = _httpsCheck.IsChecked ?? true;
        _vm.Username = _usernameBox.Text;
        _vm.Password = _passwordBox.Text;
    }

    private void UpdateNextEnabled()
    {
        if (_vm is null) return;
        SyncToVm();
        Wizard.SetNextButtonEnabled(AreCurrentFieldsValid());
    }

    private void OnWizardStepChanged(object? sender, int step)
    {
        if (_vm is null) return;
        _vm.CurrentStep = step;
        NavigateToStep(step);
    }

    private void OnWizardCancel(object? sender, EventArgs e)
    {
        _vm?.CancelCommand.Execute(null);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnWizardBack(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        SyncToVm();
        if (_vm.CurrentStep > 0)
        {
            _vm.CurrentStep--;
            NavigateToStep(_vm.CurrentStep);
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWizardFinish(object? sender, EventArgs e)
    {
        SyncToVm();
        _vm?.FinishCommand.Execute(null);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDevModeLinkClick(object? sender, RoutedEventArgs e)
    {
        try { PlatformHelper.OpenUrl("https://emulationrevival.github.io"); } catch { /* ignore */ }
    }

    public event EventHandler? CloseRequested;

    // ── UI helpers ───────────────────────────────────────────────────

    private static Avalonia.Media.Imaging.Bitmap LoadImage(string fileName)
    {
        var uri = new Uri($"{AssetsBase}{fileName}");
        var stream = AssetLoader.Open(uri);
        return new Avalonia.Media.Imaging.Bitmap(stream);
    }

    private static Border MakeCard() => new()
    {
        Background = FindBrush("SurfaceAltBrush"),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(18, 16),
        BorderBrush = FindBrush("CardBorderBrush"),
        BorderThickness = new Thickness(1)
    };

    private static TextBlock MakeFieldLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = TitleFont,
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Foreground = FindBrush("TextDimBrush"),
        Margin = new Thickness(0, 4, 0, 0)
    };

    private static TextBox MakeTextBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        FontSize = 15,
        FontFamily = BodyFont,
        Margin = new Thickness(0, 2, 0, 0),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(12, 10)
    };

    private static Border MakeSeparator() => new()
    {
        Height = 1,
        Background = FindBrush("BorderBrush"),
        Margin = new Thickness(0, 4, 0, 4)
    };

    private static (TextBlock Value, StackPanel Row) MakeSummaryRow(string label)
    {
        var valueBlock = new TextBlock
        {
            Text = "",
            FontFamily = TitleFont,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextBrush")
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new Border
                {
                    Width = 80,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = label,
                        FontFamily = BodyFont,
                        FontSize = 12,
                        Foreground = FindBrush("TextDimBrush")
                    }
                },
                new Border
                {
                    Background = FindBrush("SurfaceBrush"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6),
                    Child = valueBlock
                }
            }
        };
        return (valueBlock, row);
    }

    private static TextBlock MakeErrorText() => new()
    {
        FontFamily = BodyFont,
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.Parse("#E74C3C")),
        IsVisible = false,
        Margin = new Thickness(0, -8, 0, 0)
    };

    private static TextBlock MakeRichText(string before, (string text, bool bold) highlight, string after)
    {
        var tb = new TextBlock
        {
            FontFamily = BodyFont,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = FindBrush("TextMutedBrush")
        };
        tb.Inlines.Add(new Avalonia.Controls.Documents.Run(before)
        {
            Foreground = FindBrush("TextMutedBrush")
        });
        tb.Inlines.Add(new Avalonia.Controls.Documents.Run(highlight.text)
        {
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextBrush")
        });
        tb.Inlines.Add(new Avalonia.Controls.Documents.Run(after)
        {
            Foreground = FindBrush("TextMutedBrush")
        });
        return tb;
    }
}
