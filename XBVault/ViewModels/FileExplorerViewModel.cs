using CommunityToolkit.Mvvm.ComponentModel;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public FileExplorerViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        _xboxService.ConnectionChanged += OnConnectionChanged;
        IsConnected = _xboxService.IsConnected;
        Logger.Debug("FileExplorerViewModel initialized");
    }

    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
    }

    [ObservableProperty]
    private bool _isConnected;

    public bool ShowDisconnected => !IsConnected;
    public bool ShowContent => IsConnected;

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisconnected));
        OnPropertyChanged(nameof(ShowContent));
    }
}
