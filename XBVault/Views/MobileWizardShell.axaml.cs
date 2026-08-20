using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

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

    private static readonly FontFamily BodyFont = FontFamily.Parse("avares://XBVault/Assets/Fonts/Oxanium-400.ttf#Oxanium");
    private static readonly Uri AssetsBase = new("avares://XBVault/Assets/Views/SetupWizardWindow/");

    public MobileWizardShell()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) =>
        {
            if (_currentStep > 0)
                BackRequested?.Invoke(this, EventArgs.Empty);
            else
                CancelRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    private static Avalonia.Media.Imaging.Bitmap LoadImage(string fileName)
    {
        var uri = new Uri($"{AssetsBase}{fileName}");
        var stream = AssetLoader.Open(uri);
        return new Avalonia.Media.Imaging.Bitmap(stream);
    }

    public void InitSteps(string title, IReadOnlyList<string> stepLabels)
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
                Source = LoadImage($"setupwizard-step{i}-20.png"),
                Width = 20,
                Height = 20,
                Stretch = Stretch.None,
                VerticalAlignment = VerticalAlignment.Center
            };
            var inactiveImg = new Image
            {
                Source = LoadImage($"setupwizard-step{i}-disabled-20.png"),
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
    public void SetBackButtonEnabled(bool enabled) { }

    public void SetWizardTitle(string title) => TitleBar.Title = title;

    public void SetStepHero(string iconName, string title, string subtitle)
    {
        var stream = AssetLoader.Open(new Uri($"{AssetsBase}{iconName}"));
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
