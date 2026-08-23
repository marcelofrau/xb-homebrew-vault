using System;
using Avalonia.Controls;

namespace XBVault.Views;

public partial class MobileJobsView : UserControl
{
    private Action? _onBack;

    public MobileJobsView()
    {
        InitializeComponent();
        TitleBar.BackClicked += (_, _) => _onBack?.Invoke();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;
}
