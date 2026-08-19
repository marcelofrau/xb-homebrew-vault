using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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

    private static readonly FontFamily TitleFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-700.ttf#Oxanium");
    private static readonly FontFamily BodyFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-400.ttf#Oxanium");
    private static readonly Uri AssetsBase = new("avares://XBVault/Assets/Views/SetupWizardWindow/");

    private static IBrush FindBrush(string name) => (IBrush)Application.Current!.FindResource(name)!;

    public MobileSetupWizardView()
    {
        InitializeComponent();

        _addressBox = MakeTextBox("e.g. 192.168.1.100");
        _portBox = MakeTextBox("11443");
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
        _passwordBox = new TextBox
        {
            PlaceholderText = "Password",
            FontSize = 15,
            FontFamily = BodyFont,
            PasswordChar = '*',
            Margin = new Thickness(0, 4, 0, 0)
        };
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
        Wizard.SetBackButtonVisible(false);

        _addressBox.TextChanged += (_, _) => UpdateNextEnabled();
        _portBox.TextChanged += (_, _) => UpdateNextEnabled();
        _usernameBox.TextChanged += (_, _) => UpdateNextEnabled();
        _passwordBox.TextChanged += (_, _) => UpdateNextEnabled();
        _httpsCheck.PropertyChanged += (_, _) => UpdateNextEnabled();

        ShowWelcome();
    }

    // ── Step 0: Welcome ──────────────────────────────────────────────
    private void ShowWelcome()
    {
        Wizard.SetStepHero("setupwizard-wizard-100.png", "Welcome", "Xbox Developer Mode Setup");

        var content = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Welcome icon
        content.Children.Add(new Image
        {
            Source = LoadImage("setupwizard-welcome-48.png"),
            Width = 72, Height = 72,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        // Title
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

        // Description card
        var descCard = MakeCard();
        var descStack = new StackPanel { Spacing = 8 };
        descStack.Children.Add(MakeRichText(
            "This wizard will help you set up the connection to your ",
            ("Xbox in Developer Mode", true),
            ". Before you begin, make sure your Xbox is in "));
        descStack.Children.Add(MakeRichText(
            "Developer Mode",
            ("Developer Mode", true),
            " and turned on."));
        descCard.Child = descStack;
        content.Children.Add(descCard);

        // Learn more link
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

        // Separator
        content.Children.Add(MakeSeparator());

        // Hint
        content.Children.Add(new TextBlock
        {
            Text = "Tap Next to begin.",
            FontFamily = BodyFont,
            FontSize = 13,
            Foreground = FindBrush("TextDimBrush"),
            FontStyle = FontStyle.Italic,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        Wizard.SetStepContent(0, content);
        Wizard.SetBackButtonVisible(false);
        Wizard.SetFinishMode(false);
        Wizard.SetNextButtonEnabled(true);
    }

    // ── Step 1: Console ──────────────────────────────────────────────
    private void ShowConsole()
    {
        Wizard.SetStepHero("setupwizard-wizard-100.png", "Xbox Console", "Enter your Xbox's network address");

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        var card = MakeCard();
        var cardContent = new StackPanel { Spacing = 12 };
        cardContent.Children.Add(MakeFieldLabel("IP Address"));
        cardContent.Children.Add(_addressBox);
        cardContent.Children.Add(MakeFieldLabel("Port"));
        cardContent.Children.Add(_portBox);
        cardContent.Children.Add(_httpsCheck);
        card.Child = cardContent;
        content.Children.Add(card);

        // Info tip
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

        Wizard.SetStepContent(1, content);
        Wizard.SetBackButtonVisible(false);
        Wizard.SetFinishMode(false);
        UpdateNextEnabled();
    }

    // ── Step 2: Authentication ───────────────────────────────────────
    private void ShowAuth()
    {
        Wizard.SetStepHero("setupwizard-wizard-100.png", "Authentication", "Enter your Device Portal credentials");

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        var card = MakeCard();
        var cardContent = new StackPanel { Spacing = 12 };
        cardContent.Children.Add(MakeFieldLabel("Username"));
        cardContent.Children.Add(_usernameBox);
        cardContent.Children.Add(MakeFieldLabel("Password"));
        cardContent.Children.Add(_passwordBox);
        card.Child = cardContent;
        content.Children.Add(card);

        // Info tip
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

        Wizard.SetStepContent(2, content);
        Wizard.SetBackButtonVisible(true);
        Wizard.SetFinishMode(false);
        UpdateNextEnabled();
    }

    // ── Step 3: Ready ────────────────────────────────────────────────
    private void ShowReady()
    {
        Wizard.SetStepHero("setupwizard-wizard-100.png", "Ready to Connect", "Review your settings");

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        // Summary card
        var card = MakeCard();
        var cardStack = new StackPanel { Spacing = 10 };
        cardStack.Children.Add(MakeSummaryRow("Address", _addressBox.Text ?? ""));
        cardStack.Children.Add(MakeSummaryRow("Port", _portBox.Text ?? ""));
        cardStack.Children.Add(MakeSummaryRow("HTTPS", _httpsCheck.IsChecked == true ? "Yes" : "No"));
        cardStack.Children.Add(MakeSummaryRow("Username", _usernameBox.Text ?? ""));
        card.Child = cardStack;
        content.Children.Add(card);

        Wizard.SetStepContent(3, content);
        Wizard.SetBackButtonVisible(true);
        Wizard.SetFinishMode(true, "Finish");
        Wizard.SetNextButtonEnabled(true);
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
        Wizard.SetNextButtonEnabled(_vm.CanGoNext);
    }

    private void OnWizardStepChanged(object? sender, int step)
    {
        if (_vm is null) return;
        _vm.CurrentStep = step;
        switch (step)
        {
            case 0: ShowWelcome(); break;
            case 1: ShowConsole(); break;
            case 2: ShowAuth(); break;
            case 3: ShowReady(); break;
        }
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
            switch (_vm.CurrentStep)
            {
                case 0: ShowWelcome(); break;
                case 1: ShowConsole(); break;
                case 2: ShowAuth(); break;
                case 3: ShowReady(); break;
            }
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

    private static StackPanel MakeSummaryRow(string label, string value)
    {
        return new StackPanel
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
                    Child = new TextBlock
                    {
                        Text = value,
                        FontFamily = TitleFont,
                        FontSize = 13,
                        FontWeight = FontWeight.Bold,
                        Foreground = FindBrush("TextBrush")
                    }
                }
            }
        };
    }

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
