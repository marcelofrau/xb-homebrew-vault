using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XBVault.Views;

public partial class MobileTitleBar : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MobileTitleBar, string?>(nameof(Title));

    public static readonly StyledProperty<bool> ShowBackButtonProperty =
        AvaloniaProperty.Register<MobileTitleBar, bool>(nameof(ShowBackButton), true);

    public static readonly StyledProperty<bool> ShowAppIconProperty =
        AvaloniaProperty.Register<MobileTitleBar, bool>(nameof(ShowAppIcon), true);

    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<MobileTitleBar, object?>(nameof(RightContent));

    public static readonly StyledProperty<object?> FarRightContentProperty =
        AvaloniaProperty.Register<MobileTitleBar, object?>(nameof(FarRightContent));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowBackButton
    {
        get => GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public bool ShowAppIcon
    {
        get => GetValue(ShowAppIconProperty);
        set => SetValue(ShowAppIconProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public object? FarRightContent
    {
        get => GetValue(FarRightContentProperty);
        set => SetValue(FarRightContentProperty, value);
    }

    public event EventHandler? BackClicked;

    public MobileTitleBar()
    {
        InitializeComponent();

        RightContentProperty.Changed.AddClassHandler<MobileTitleBar>((x, _) =>
            x.RightContentSlot.IsVisible = x.RightContent is not null);
        FarRightContentProperty.Changed.AddClassHandler<MobileTitleBar>((x, _) =>
            x.FarRightContentSlot.IsVisible = x.FarRightContent is not null);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackClicked?.Invoke(this, EventArgs.Empty);
    }
}
