using System;
using Avalonia.Controls;
using XBVault.ViewModels;

namespace XBVault.Views;

public partial class MobileLoopbackView : UserControl
{
    private Action? _onBack;

    public MobileLoopbackView()
    {
        InitializeComponent();
    }

    public void SetViewModel(LoopbackExemptViewModel vm)
    {
        DataContext = vm;
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;
}
