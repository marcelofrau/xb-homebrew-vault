using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.ViewModels;

public partial class ConfirmViewModel : ObservableObject
{
    public ConfirmViewModel(string title, string message, string confirmText, string cancelText, bool isExit = false)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsExit = isExit;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public bool IsExit { get; }
    public bool Confirmed { get; private set; }
    public event Action<bool>? Completed;

    [RelayCommand]
    private void Confirm()
    {
        Confirmed = true;
        Completed?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        Completed?.Invoke(false);
    }
}
