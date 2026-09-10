using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Controls.Platform;
using XBVault.Services;

namespace XBVault.Views;

public partial class MobileWizardShell : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler<int>? StepChanged;
    public event EventHandler? FinishClicked;

    private readonly List<(StackPanel Container, Image ActiveImg, Image InactiveImg, TextBlock Label)> _stepIndicators = [];
    private int _currentStep;
    private int _totalSteps;
    private int _maxVisitedStep;
    private bool _isFinishMode;
    private IInputPane? _inputPane;
    private bool _keyboardOpen;

    private static readonly FontFamily BodyFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-400.ttf#Oxanium");
    private static readonly Uri AssetsBase = new("avares://XBVault/Assets/Views/SetupWizardWindow/");

    public MobileWizardShell()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) =>
        {
            // Back button in title bar always closes the wizard
            CancelRequested?.Invoke(this, EventArgs.Empty);
        };

        StepContent.GotFocus += (_, e) =>
        {
            if (e.Source is Control ctrl)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ctrl.BringIntoView());
        };
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Avalonia's IInputPane (backed by the platform IME observer) reports the soft
        // keyboard state and its occluded rect in DIPs — no custom platform code needed.
        _inputPane = TopLevel.GetTopLevel(this)?.InputPane;
        if (_inputPane is not null)
        {
            _inputPane.StateChanged += OnInputPaneStateChanged;
            ApplyKeyboardState(_inputPane.State == InputPaneState.Open, _inputPane.OccludedRect.Height);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_inputPane is not null)
        {
            _inputPane.StateChanged -= OnInputPaneStateChanged;
            _inputPane = null;
        }
        RestoreChrome();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnInputPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        ApplyKeyboardState(e.NewState == InputPaneState.Open, e.EndRect.Height);
    }

    /// <summary>
    /// Soft keyboard came up: collapse the hero banner to a one-line title bar, shrink the
    /// title bar, and replace the breadcrumb pills with a compact "Step n of m" summary —
    /// freeing vertical space. The occluded keyboard height is reserved as extra scroll room
    /// and the focused field is pushed back into view so it never hides behind the IME.
    /// </summary>
    private void ApplyKeyboardState(bool isOpen, double keyboardHeight)
    {
        if (isOpen != _keyboardOpen)
        {
            _keyboardOpen = isOpen;
            if (isOpen)
            {
                HeroRoot.Height = 40;
                WizardStepIcon.IsVisible = false;
                WizardStepSubtitle.IsVisible = false;
                WizardStepTitle.FontSize = 12;
                WizardStepTitle.Margin = new Thickness(16, 0);
                HeroHeader.Spacing = 0;

                StepIndicatorPanel.IsVisible = false;
                StepCompactText.IsVisible = true;
                StepIndicatorBorder.Padding = new Thickness(16, 6);
                ProgressBorder.IsVisible = false;

                TitleBar.MinHeight = 36;
            }
            else
            {
                RestoreChrome();
            }
        }

        if (_keyboardOpen)
        {
            StepContent.Margin = new Thickness(16, 16, 16, 16 + keyboardHeight);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focused is Control ctrl)
                ctrl.BringIntoView();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void RestoreChrome()
    {
        HeroRoot.Height = double.NaN;
        WizardStepIcon.IsVisible = true;
        WizardStepSubtitle.IsVisible = true;
        WizardStepTitle.FontSize = 18;
        WizardStepTitle.Margin = new Thickness(0);
        HeroHeader.Spacing = 8;

        StepIndicatorPanel.IsVisible = true;
        StepCompactText.IsVisible = false;
        StepIndicatorBorder.Padding = new Thickness(16, 10);
        ProgressBorder.IsVisible = true;

        TitleBar.MinHeight = 48;

        StepContent.Margin = new Thickness(16, 24, 16, 24);
    }

    private static Avalonia.Media.Imaging.Bitmap LoadImage(string fileName, string folder)
    {
        var uri = new Uri($"avares://XBVault/Assets/Views/{folder}/{fileName}");
        var stream = AssetLoader.Open(uri);
        return new Avalonia.Media.Imaging.Bitmap(stream);
    }

    public void InitSteps(string title, IReadOnlyList<string> stepLabels, string imagePrefix = "setupwizard-step", string imageFolder = "SetupWizardWindow")
    {
        TitleBar.Title = title;
        _totalSteps = stepLabels.Count;
        _maxVisitedStep = 0;
        _stepIndicators.Clear();
        StepIndicatorPanel.Children.Clear();

        for (var i = 0; i < stepLabels.Count; i++)
        {
            if (i > 0)
            {
                var connector = new Border
                {
                    Background = (IBrush)Application.Current!.FindResource("BorderBrush")!,
                    Height = 2,
                    Width = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.5
                };
                StepIndicatorPanel.Children.Add(connector);
            }

            var activeImg = new Image
            {
                Source = LoadImage($"{imagePrefix}{i}-20.png", imageFolder),
                Width = 20,
                Height = 20,
                Stretch = Stretch.None,
                VerticalAlignment = VerticalAlignment.Center
            };
            var inactiveImg = new Image
            {
                Source = LoadImage($"{imagePrefix}{i}-disabled-20.png", imageFolder),
                Width = 20,
                Height = 20,
                Stretch = Stretch.None,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false
            };

            var grid = new Grid { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(inactiveImg);
            grid.Children.Add(activeImg);

            var label = new TextBlock
            {
                Text = stepLabels[i],
                FontFamily = BodyFont,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = (IBrush)Application.Current!.FindResource("TextMutedBrush")!,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            stack.Children.Add(grid);
            stack.Children.Add(label);

            var stepIndex = i;
            stack.Tapped += (_, _) => OnStepIndicatorTapped(stepIndex);

            StepIndicatorPanel.Children.Add(stack);
            _stepIndicators.Add((stack, activeImg, inactiveImg, label));
        }

        SetActiveStep(0);
    }

    public void SetStepContent(int stepIndex, object content)
    {
        StepContent.Content = content;
        SetActiveStep(stepIndex);
    }

    public void SetFinishMode(bool showFinish, string finishText = "Finish")
    {
        _isFinishMode = showFinish;
        NextBtnLabel.Text = showFinish ? finishText : "Next";
    }

    public void SetBackButtonVisible(bool visible) => TitleBar.ShowBackButton = visible;
    public void SetNextButtonEnabled(bool enabled) => NextBtn.IsEnabled = enabled;
    public void SetWizardTitle(string title) => TitleBar.Title = title;

    public void SetStepHero(string iconName, string title, string subtitle, string? folderOverride = null)
    {
        var baseUri = folderOverride is not null
            ? $"avares://XBVault/Assets/Views/{folderOverride}/"
            : AssetsBase.ToString();
        var stream = AssetLoader.Open(new Uri($"{baseUri}{iconName}"));
        WizardStepIcon.Source = new Avalonia.Media.Imaging.Bitmap(stream);
        WizardStepIcon.IsVisible = true;
        WizardStepTitle.Text = title;
        WizardStepSubtitle.Text = subtitle;
    }

    public void UpdateProgress()
    {
        if (_totalSteps <= 1) return;
        var progress = (double)(_currentStep + 1) / _totalSteps;
        var maxWidth = Bounds.Width > 0 ? Bounds.Width - 32 : 300;
        ProgressFill.Width = maxWidth * progress;
    }

    private void OnStepIndicatorTapped(int stepIndex)
    {
        if (stepIndex <= _maxVisitedStep && stepIndex != _currentStep)
            StepChanged?.Invoke(this, stepIndex);
    }

    private void SetActiveStep(int stepIndex)
    {
        _currentStep = stepIndex;
        if (stepIndex > _maxVisitedStep) _maxVisitedStep = stepIndex;

        PrevBtnLabel.Text = stepIndex == 0 ? "Cancel" : "Previous";
        PrevBtn.Classes.Clear();
        PrevBtn.Classes.Add("Danger");

        // Step 0: show app icon, hide back button
        // Steps 1+: hide app icon, show back button
        TitleBar.ShowAppIcon = stepIndex == 0;
        TitleBar.ShowBackButton = stepIndex > 0;

        for (var i = 0; i < _stepIndicators.Count; i++)
        {
            var (_, activeImg, inactiveImg, label) = _stepIndicators[i];
            var container = _stepIndicators[i].Container;
            var isActive = i == stepIndex;
            var isPast = i < stepIndex;

            activeImg.IsVisible = isActive;
            inactiveImg.IsVisible = !isActive;
            label.Foreground = (IBrush)Application.Current!.FindResource(
                isActive ? "AccentBrush" : isPast ? "TextMutedBrush" : "TextDimBrush")!;
            label.Opacity = isActive ? 1.0 : isPast ? 0.7 : 0.5;
            container.Cursor = i <= _maxVisitedStep
                ? new Cursor(StandardCursorType.Hand)
                : new Cursor(StandardCursorType.Arrow);
            container.Opacity = i <= _maxVisitedStep ? 1.0 : 0.6;
        }

        UpdateProgress();
        UpdateCompactStepText();
    }

    private void UpdateCompactStepText()
    {
        if (_stepIndicators.Count == 0) return;
        var label = _stepIndicators[_currentStep].Label.Text;
        StepCompactText.Text = $"Step {_currentStep + 1} of {_totalSteps} — {label}";
    }

    private void OnPrevClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep == 0)
            CancelRequested?.Invoke(this, EventArgs.Empty);
        else
            BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNavBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 0) BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNavNextClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep < _totalSteps - 1)
            StepChanged?.Invoke(this, _currentStep + 1);
        else
            FinishClicked?.Invoke(this, EventArgs.Empty);
    }
}
