using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XBVault.Views;

public partial class MobileInfoDialogView : UserControl
{
    private Action? _onBack;

    public MobileInfoDialogView()
    {
        InitializeComponent();
    }

    public void SetOnBack(Action onBack) => _onBack = onBack;

    public void TriggerOk() => ((MobileInfoDialogViewModel)DataContext!).OkCommand.Execute(null);
}

public partial class MobileInfoDialogViewModel : ObservableObject
{
    private TaskCompletionSource? _tcs;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string? _details;

    public bool HasDetails => !string.IsNullOrEmpty(Details);

    public Task WaitForResult()
    {
        _tcs = new TaskCompletionSource();
        return _tcs.Task;
    }

    [RelayCommand]
    private void Ok()
    {
        _tcs?.TrySetResult();
    }
}
