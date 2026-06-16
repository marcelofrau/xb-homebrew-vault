using CommunityToolkit.Mvvm.ComponentModel;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public MainViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        UpdateConnectionStatus();
    }

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private string _connectionStatusText = "Not configured";

    [ObservableProperty]
    private bool _isXboxConnected;

    public bool IsBrowseActive => SelectedTab == 0;
    public bool IsInstalledActive => SelectedTab == 1;
    public bool IsSettingsActive => SelectedTab == 2;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsBrowseActive));
        OnPropertyChanged(nameof(IsInstalledActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    public void UpdateConnectionStatus()
    {
        IsXboxConnected = _xboxService.IsConfigured;
        ConnectionStatusText = _xboxService.IsConfigured ? "Connected" : "Not configured";
    }
}
