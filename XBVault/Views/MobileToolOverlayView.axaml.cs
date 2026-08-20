using System;
using Avalonia.Controls;

namespace XBVault.Views;

public partial class MobileToolOverlayView : UserControl
{
    private Action? _onBack;

    public MobileToolOverlayView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
    }

    public void SetTitle(string title) => TitleBar.Title = title;

    public void SetContent(Control content) => ContentSlot.Content = content;

    public void SetOnBack(Action onBack) => _onBack = onBack;
}
