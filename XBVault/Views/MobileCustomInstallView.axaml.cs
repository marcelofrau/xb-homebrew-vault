using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using XBVault.Controls;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileCustomInstallView : UserControl
{
    private CustomInstallViewModel? _vm;

    private readonly StackPanel _step0Content;
    private readonly StackPanel _step1Content;
    private readonly StackPanel _step2Content;
    private readonly StackPanel _step3Content;

    private TextBox _sourceUrlBox = null!;
    private TextBlock _selectedFileText = null!;
    private TextBlock _statusLabel = null!;
    private TextBlock _analysisText = null!;
    private TextBlock _depCountText = null!;
    private StackPanel _depListPanel = null!;
    private CheckBox _cleanInstallCheck = null!;
    private TextBlock _summaryPackage = null!;
    private TextBlock _summaryDeps = null!;
    private ProgressBar _installProgress = null!;
    private TextBlock _installStatus = null!;
    private TextBlock _installFile = null!;
    private StackPanel _resultSuccess = null!;
    private StackPanel _resultFailure = null!;
    private TextBlock _resultMessage = null!;
    private Button _browseBtn = null!;
    private Button _analyzeBtn = null!;
    private Button _addDepBtn = null!;
    private Button _installBtn = null!;

    private static readonly FontFamily TitleFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-700.ttf#Oxanium");
    private static readonly FontFamily BodyFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-400.ttf#Oxanium");
    private static readonly Uri AssetsBase = new("avares://XBVault/Assets/Views/CustomInstallWindow/");

    private static IBrush FindBrush(string name) => (IBrush)Application.Current!.FindResource(name)!;

    public MobileCustomInstallView()
    {
        InitializeComponent();
        _step0Content = BuildStep0();
        _step1Content = BuildStep1();
        _step2Content = BuildStep2();
        _step3Content = BuildStep3();
    }

    public void SetViewModel(CustomInstallViewModel vm)
    {
        _vm = vm;

        _browseBtn.Command = vm.BrowseFileCommand;
        _analyzeBtn.Command = vm.AnalyzeCommand;
        _sourceUrlBox.TextChanged += (_, _) => vm.SourceUrl = _sourceUrlBox.Text ?? "";
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CustomInstallViewModel.SourcePath))
            {
                var path = vm.SourcePath ?? "";
                _selectedFileText.Text = path;
                _selectedFileText.IsVisible = !string.IsNullOrWhiteSpace(path);
            }
        };
        _addDepBtn.Command = vm.AddDepCommand;
        _installBtn.Command = vm.InstallCommand;

        vm.DepItems.CollectionChanged += (_, _) => UpdateDependencyList();

        Wizard.BackRequested += OnWizardBack;
        Wizard.CancelRequested += OnWizardCancel;
        Wizard.StepChanged += OnWizardStepChanged;
        Wizard.FinishClicked += OnWizardFinish;

        Wizard.InitSteps("Custom Install", ["Source", "Analysis", "Dependencies", "Install"], "custominstall-step", "CustomInstallWindow");

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CustomInstallViewModel.CurrentStep))
                NavigateToStep(vm.CurrentStep);
            else if (e.PropertyName == nameof(CustomInstallViewModel.IsAnalyzing))
                UpdateAnalysisState();
            else if (e.PropertyName == nameof(CustomInstallViewModel.IsInstalling))
                UpdateInstallState();
            else if (e.PropertyName == nameof(CustomInstallViewModel.InstallComplete))
                UpdateInstallState();
            else if (e.PropertyName == nameof(CustomInstallViewModel.InstallProgress))
                _installProgress.Value = vm.InstallProgress;
            else if (e.PropertyName == nameof(CustomInstallViewModel.InstallStatus))
                _installStatus.Text = vm.InstallStatus ?? "";
            else if (e.PropertyName == nameof(CustomInstallViewModel.CurrentFile))
                _installFile.Text = vm.CurrentFile ?? "";
            else if (e.PropertyName == nameof(CustomInstallViewModel.StatusText))
                _statusLabel.Text = vm.StatusText ?? "";
            else if (e.PropertyName == nameof(CustomInstallViewModel.AnalysisResultText))
                _analysisText.Text = vm.AnalysisResultText ?? "";
            else if (e.PropertyName == nameof(CustomInstallViewModel.MainPackageName))
                _summaryPackage.Text = vm.MainPackageName ?? "";
            else if (e.PropertyName == nameof(CustomInstallViewModel.DependencyText))
                _summaryDeps.Text = vm.DependencyText;
            else if (e.PropertyName == nameof(CustomInstallViewModel.InstallSuccess))
                UpdateInstallResult();
            else if (e.PropertyName == nameof(CustomInstallViewModel.InstallResultMessage))
                _resultMessage.Text = vm.InstallResultMessage ?? "";
            else if (e.PropertyName == nameof(CustomInstallViewModel.CanGoNext))
                UpdateNextEnabled();
        };

        NavigateToStep(0);
    }

    private void NavigateToStep(int step)
    {
        if (_vm is null) return;

        switch (step)
        {
            case 0:
                Wizard.SetStepHero("custominstall-wizard-100.png", "Choose Source",
                    "Select a local package file or enter a download URL", "CustomInstallWindow");
                Wizard.SetStepContent(0, _step0Content);
                Wizard.SetFinishMode(false);
                UpdateNextEnabled();
                break;
            case 1:
                Wizard.SetStepHero("custominstall-analyze-100.png", "Analyzing",
                    "Examining package structure and dependencies", "CustomInstallWindow");
                Wizard.SetStepContent(1, _step1Content);
                Wizard.SetFinishMode(false);
                Wizard.SetNextButtonEnabled(false);
                UpdateAnalysisState();
                break;
            case 2:
                Wizard.SetStepHero("custominstall-packages-100.png", "Review Packages",
                    "Review the main package and dependencies before installing", "CustomInstallWindow");
                Wizard.SetStepContent(2, _step2Content);
                Wizard.SetFinishMode(false);
                UpdateDependencyList();
                Wizard.SetNextButtonEnabled(_vm.CanGoNext);
                break;
            case 3:
                Wizard.SetStepHero(_vm.IsInstalling ? "custominstall-download-100.png" :
                    _vm.InstallComplete ? (_vm.InstallSuccess ? "custominstall-success-100.png" : "custominstall-failure-100.png") :
                    "custominstall-install-20.png", "Install",
                    _vm.IsInstalling ? "Installing packages..." :
                    _vm.InstallComplete ? (_vm.InstallSuccess ? "Installation successful!" : "Installation failed") :
                    "Ready to install", "CustomInstallWindow");
                Wizard.SetStepContent(3, _step3Content);
                UpdateInstallState();
                UpdateInstallResult();
                _summaryPackage.Text = _vm.MainPackageName ?? "";
                _summaryDeps.Text = _vm.DependencyText;
                break;
        }
    }

    private void UpdateNextEnabled()
    {
        if (_vm is null) return;
        Wizard.SetNextButtonEnabled(_vm.CanGoNext);
    }

    private void UpdateAnalysisState()
    {
        if (_vm is null) return;
        _statusLabel.Text = _vm.StatusText ?? "";
        _analysisText.Text = _vm.AnalysisResultText ?? "";
    }

    private void UpdateInstallState()
    {
        if (_vm is null) return;
        var installing = _vm.IsInstalling;
        var complete = _vm.InstallComplete;
        var showSummary = !installing && !complete;

        Wizard.SetNextButtonEnabled(complete && _vm.InstallSuccess);

        // Hide all sub-panels, show the right one
        foreach (var child in _step3Content.Children)
        {
            if (child is Panel p)
                p.IsVisible = false;
        }

        if (installing)
        {
            _installProgress.Value = _vm.InstallProgress;
            _installStatus.Text = _vm.InstallStatus ?? "";
            _installFile.Text = _vm.CurrentFile ?? "";
            _installingPanel.IsVisible = true;
        }
        else if (complete)
        {
            UpdateInstallResult();
            _resultPanel.IsVisible = true;
            Wizard.SetFinishMode(true, "Done");
            Wizard.SetStepHero(_vm.InstallSuccess ? "custominstall-success-100.png" : "custominstall-failure-100.png",
                _vm.InstallSuccess ? "Success!" : "Failed",
                _vm.InstallResultMessage ?? "", "CustomInstallWindow");
        }
        else
        {
            _summaryPanel.IsVisible = true;
            Wizard.SetFinishMode(false);
        }
    }

    private void UpdateInstallResult()
    {
        if (_vm is null) return;
        _resultSuccess.IsVisible = _vm.InstallSuccess;
        _resultFailure.IsVisible = !_vm.InstallSuccess;
        _resultMessage.Text = _vm.InstallResultMessage ?? "";
    }

    private void UpdateDependencyList()
    {
        if (_vm is null) return;
        _depCountText.Text = _vm.DependencyText;
        _depListPanel.Children.Clear();
        foreach (var dep in _vm.DepItems)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4) };
            row.Children.Add(new TextBlock
            {
                Text = dep.DisplayName,
                FontFamily = BodyFont,
                FontSize = 13,
                Foreground = FindBrush("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260
            });
            var removeBtn = new Button
            {
                Padding = new Thickness(8, 4),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = new TextBlock
                {
                    Text = "x",
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Foreground = FindBrush("DangerBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var depRef = dep;
            removeBtn.Click += (_, _) =>
            {
                _vm.RemoveDepCommand.Execute(depRef);
                UpdateDependencyList();
            };
            row.Children.Add(removeBtn);
            _depListPanel.Children.Add(row);
        }
    }

    // ── Step builders ─────────────────────────────────────────────

    private StackPanel BuildStep0()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

        // File source card
        var fileCard = MakeCard();
        var fileHeader = new TextBlock
        {
            Text = "LOCAL FILE",
            FontFamily = TitleFont,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextDimBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var fileContent = new StackPanel { Spacing = 10 };
        fileContent.Children.Add(fileHeader);
        _selectedFileText = new TextBlock
        {
            FontSize = 13,
            Foreground = FindBrush("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        fileContent.Children.Add(_selectedFileText);
        var browseBtn = new Button
        {
            Padding = new Thickness(16, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        browseBtn.Content = MakeIconTextRow("custominstall-file-20.png", "Browse File...");
        _browseBtn = browseBtn;
        fileContent.Children.Add(browseBtn);
        _statusLabel = new TextBlock
        {
            FontSize = 12,
            Foreground = FindBrush("TextDimBrush"),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        fileContent.Children.Add(_statusLabel);
        fileCard.Child = fileContent;
        content.Children.Add(fileCard);

        // URL source card
        var urlCard = MakeCard();
        var urlContent = new StackPanel { Spacing = 10 };
        var urlLabel = new TextBlock
        {
            Text = "DOWNLOAD URL",
            FontFamily = TitleFont,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextDimBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        urlContent.Children.Add(urlLabel);
        _sourceUrlBox = MakeTextBox("https://example.com/package.appx");
        urlContent.Children.Add(_sourceUrlBox);
        urlCard.Child = urlContent;
        _urlPanel = urlCard;
        content.Children.Add(urlCard);

        // Analyze button
        var analyzeBtn = new Button
        {
            Padding = new Thickness(20, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        analyzeBtn.Content = MakeIconTextRow("custominstall-analyze-20.png", "Analyze Package");
        _analyzeBtn = analyzeBtn;
        content.Children.Add(analyzeBtn);

        return content;
    }

    private Border? _urlPanel;

    private void UpdateSourceVisibility()
    {
        if (_urlPanel is null) return;
        _urlPanel.IsVisible = true;
    }

    private StackPanel BuildStep1()
    {
        var content = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };

        content.Children.Add(new CdSpinner
        {
            HorizontalAlignment = HorizontalAlignment.Center
        });

        _analysisText = new TextBlock
        {
            FontSize = 14,
            Foreground = FindBrush("TextMutedBrush"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(_analysisText);

        return content;
    }

    private StackPanel BuildStep2()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        // Main package card
        var pkgCard = MakeCard();
        var pkgStack = new StackPanel { Spacing = 6 };
        var pkgHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var pkgIcon = new Border
        {
            Width = 40, Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = FindBrush("AccentDimBrush"),
            Child = new Image
            {
                Source = LoadImage("custominstall-package-20.png"),
                Width = 24, Height = 24,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        pkgHeader.Children.Add(pkgIcon);
        var pkgInfo = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        _summaryPackage = new TextBlock
        {
            FontFamily = TitleFont,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        pkgInfo.Children.Add(_summaryPackage);
        _depCountText = new TextBlock
        {
            FontSize = 12,
            Foreground = FindBrush("TextMutedBrush")
        };
        pkgInfo.Children.Add(_depCountText);
        pkgHeader.Children.Add(pkgInfo);
        pkgStack.Children.Add(pkgHeader);
        pkgCard.Child = pkgStack;
        content.Children.Add(pkgCard);

        // Dependencies header
        var depHeader = new TextBlock
        {
            Text = "DEPENDENCIES",
            FontFamily = TitleFont,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextMutedBrush"),
            Margin = new Thickness(4, 8, 0, 0)
        };
        content.Children.Add(depHeader);

        // Dependencies list
        var depCard = MakeCard();
        _depListPanel = new StackPanel { Spacing = 4 };
        var depEmpty = new TextBlock
        {
            Text = "No dependencies detected.",
            FontSize = 13,
            Foreground = FindBrush("TextMutedBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8)
        };
        _depListPanel.Children.Add(depEmpty);
        depCard.Child = _depListPanel;
        content.Children.Add(depCard);

        // Add dependency button
        var addDepBtn = new Button
        {
            Padding = new Thickness(16, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        addDepBtn.Content = MakeIconTextRow("custominstall-add-20.png", "Add dependency files");
        _addDepBtn = addDepBtn;
        content.Children.Add(addDepBtn);

        // Clean install checkbox
        _cleanInstallCheck = new CheckBox
        {
            Content = "Clean install (uninstall existing version first)",
            FontSize = 13,
            FontFamily = BodyFont,
            Foreground = FindBrush("TextBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        _cleanInstallCheck.PropertyChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PerformCleanInstall = _cleanInstallCheck.IsChecked ?? false;
        };
        content.Children.Add(_cleanInstallCheck);

        return content;
    }

    private StackPanel _installingPanel = null!;
    private StackPanel _summaryPanel = null!;
    private StackPanel _resultPanel = null!;

    private StackPanel BuildStep3()
    {
        var root = new StackPanel { Spacing = 12 };

        // Summary (before install)
        _summaryPanel = new StackPanel { Spacing = 10 };
        var summaryCard = MakeCard();
        var summaryStack = new StackPanel { Spacing = 8 };
        summaryStack.Children.Add(MakeSummaryRow("Package:", _summaryPackage = new TextBlock
        {
            FontFamily = TitleFont, FontSize = 13, FontWeight = FontWeight.Bold,
            Foreground = FindBrush("TextBrush")
        }));
        summaryStack.Children.Add(MakeSummaryRow("Dependencies:", _summaryDeps = new TextBlock
        {
            FontSize = 13, Foreground = FindBrush("TextMutedBrush")
        }));
        summaryCard.Child = summaryStack;
        _summaryPanel.Children.Add(summaryCard);

        // Install button
        var installBtn = new Button
        {
            Classes = { "Accent" },
            Padding = new Thickness(20, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        installBtn.Content = MakeIconTextRow("custominstall-install-20.png", "Start Installation");
        _installBtn = installBtn;
        _summaryPanel.Children.Add(installBtn);
        root.Children.Add(_summaryPanel);

        // Installing progress
        _installingPanel = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        _installingPanel.Children.Add(new CdSpinner
        {
            Width = 56, Height = 56,
            ShowText = false,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _installStatus = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = FindBrush("AccentBrush"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _installingPanel.Children.Add(_installStatus);
        _installProgress = new ProgressBar
        {
            Maximum = 1,
            Height = 6,
            Foreground = FindBrush("AccentBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _installingPanel.Children.Add(_installProgress);
        _installFile = new TextBlock
        {
            FontSize = 11,
            Foreground = FindBrush("TextDimBrush"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _installingPanel.Children.Add(_installFile);
        root.Children.Add(_installingPanel);

        // Result
        _resultPanel = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        _resultSuccess = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        _resultSuccess.Children.Add(new Image
        {
            Source = LoadImage("custominstall-success-100.png"),
            Width = 80, Height = 80,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _resultSuccess.Children.Add(new TextBlock
        {
            Text = "Install complete!",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("SuccessBrush"),
            TextAlignment = TextAlignment.Center
        });
        _resultPanel.Children.Add(_resultSuccess);

        _resultFailure = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, IsVisible = false };
        _resultFailure.Children.Add(new Image
        {
            Source = LoadImage("custominstall-failure-100.png"),
            Width = 80, Height = 80,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _resultFailure.Children.Add(new TextBlock
        {
            Text = "Install failed",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = FindBrush("DangerBrush"),
            TextAlignment = TextAlignment.Center
        });
        _resultMessage = new TextBlock
        {
            FontSize = 13,
            Foreground = FindBrush("TextMutedBrush"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320
        };
        _resultFailure.Children.Add(_resultMessage);
        _resultPanel.Children.Add(_resultFailure);

        root.Children.Add(_resultPanel);
        return root;
    }

    private static Border MakeSummaryRow(string label, TextBlock valueBlock)
    {
        return new Border
        {
            Background = FindBrush("SurfaceAltBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontFamily = BodyFont,
                        FontSize = 12,
                        Foreground = FindBrush("TextDimBrush"),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    valueBlock
                }
            }
        };
    }

    // ── Navigation handlers ──────────────────────────────────────

    private void OnWizardStepChanged(object? sender, int step)
    {
        if (_vm is null) return;

        if (step == 1 && _vm.CurrentStep == 0)
        {
            var hasSource = !string.IsNullOrWhiteSpace(_vm.SourcePath) || !string.IsNullOrWhiteSpace(_vm.SourceUrl);
            if (hasSource && !_vm.IsAnalyzing)
            {
                _vm.AnalyzeCommand.Execute(null);
                return;
            }
            if (!hasSource)
                return;
        }

        _vm.CurrentStep = step;
        NavigateToStep(step);
    }

    private void OnWizardBack(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        if (_vm.CurrentStep > 0)
        {
            _vm.CancelAnalysis();
            _vm.CurrentStep = 0;
            NavigateToStep(0);
        }
        else
        {
            _vm.CancelCommand.Execute(null);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWizardCancel(object? sender, EventArgs e)
    {
        _vm?.CancelCommand.Execute(null);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnWizardFinish(object? sender, EventArgs e)
    {
        if (_vm?.InstallComplete == true)
        {
            _vm.CloseCommand.Execute(null);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CloseRequested;

    // ── UI helpers ──────────────────────────────────────────────

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

    private static TextBox MakeTextBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        FontSize = 15,
        FontFamily = BodyFont,
        Margin = new Thickness(0, 2, 0, 0),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(12, 10)
    };

    private static StackPanel MakeIconTextRow(string iconName, string text)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        row.Children.Add(new Image
        {
            Source = LoadImage(iconName),
            Width = 18, Height = 18,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = BodyFont,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }
}
