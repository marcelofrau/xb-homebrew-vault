using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.ViewModels;

public partial class ConfirmViewModel : ObservableObject
{
    public ConfirmViewModel(string title, string message, string confirmText, string cancelText, string? iconSource = null)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        if (!string.IsNullOrEmpty(iconSource))
            Icon = new Bitmap(AssetLoader.Open(new Uri(iconSource)));
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public Bitmap? Icon { get; }
    public bool HasIcon => Icon is not null;
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
