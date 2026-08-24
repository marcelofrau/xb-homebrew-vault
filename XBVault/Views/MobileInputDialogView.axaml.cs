#nullable enable
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.Views;

public partial class MobileInputDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Input";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _value = "";
    [ObservableProperty] private string _confirmText = "OK";
    [ObservableProperty] private string _cancelText = "Cancel";

    private readonly TaskCompletionSource<string?> _tcs = new();
    public Task<string?> WaitForResult() => _tcs.Task;

    [RelayCommand]
    private void Confirm()
    {
        _tcs.TrySetResult(Value);
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(null);
    }
}

public partial class MobileInputDialogView : UserControl
{
#pragma warning disable CS0067 // Event is used in App.axaml.cs
    public event EventHandler? BackRequested;
#pragma warning restore CS0067

    public MobileInputDialogView()
    {
        InitializeComponent();
    }
}
