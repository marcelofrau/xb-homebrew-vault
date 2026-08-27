using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.Views;

public partial class MobileConfirmDialogView : UserControl
{
    private Action? _onBack;

    public MobileConfirmDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => LoadIcon();
    }

    private void LoadIcon()
    {
        if (DataContext is not MobileConfirmDialogViewModel vm || string.IsNullOrEmpty(vm.ImageSource))
        {
            DialogIcon.Source = null;
            return;
        }
        try
        {
            DialogIcon.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri(vm.ImageSource)));
        }
        catch
        {
            // fallback — icon asset missing, dialog renders without icon
            DialogIcon.Source = null;
        }
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;
}

public partial class MobileConfirmDialogViewModel : ObservableObject
{
    private TaskCompletionSource<bool>? _tcs;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string? _imageSource;

    [ObservableProperty]
    private string _confirmText = "OK";

    [ObservableProperty]
    private string _cancelText = "Cancel";

    public Task<bool> WaitForResult()
    {
        _tcs = new TaskCompletionSource<bool>();
        return _tcs.Task;
    }

    [RelayCommand]
    private void Confirm()
    {
        _tcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs?.TrySetResult(false);
    }
}
