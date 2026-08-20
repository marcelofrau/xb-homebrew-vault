using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XBVault.Views;

public partial class MobileToolResultView : UserControl
{
    public MobileToolResultView()
    {
        InitializeComponent();
    }
}

public partial class MobileToolResultViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _contentText;

    public bool HasContent => !string.IsNullOrEmpty(ContentText) && !IsLoading;
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage) && !IsLoading;

    partial void OnContentTextChanged(string? value)
    {
        OnPropertyChanged(nameof(HasContent));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }
}
