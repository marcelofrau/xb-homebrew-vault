using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.ViewModels;

public partial class ConfirmViewModel : ObservableObject
{
    private static readonly Uri DefaultMessageIconUri = new("avares://XBVault/Assets/Views/ErrorDialog/errordialog-warn-48.png");

    public ConfirmViewModel(string title, string message, string confirmText, string cancelText, string? iconSource = null, string? messageIconSource = null, bool isDestructive = false)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDestructive = isDestructive;

        if (!string.IsNullOrEmpty(iconSource))
            Icon = new Bitmap(AssetLoader.Open(new Uri(iconSource)));

        var msgUri = !string.IsNullOrEmpty(messageIconSource)
            ? new Uri(messageIconSource)
            : !string.IsNullOrEmpty(iconSource)
                ? new Uri(iconSource)
                : DefaultMessageIconUri;
        MessageIcon = new Bitmap(AssetLoader.Open(msgUri));
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public Bitmap? Icon { get; }
    public Bitmap MessageIcon { get; }
    public bool HasIcon => Icon is not null;
    public bool IsDestructive { get; }
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
