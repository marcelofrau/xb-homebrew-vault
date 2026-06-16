using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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

    public Action? ShowAboutAction { get; set; }
    public Func<Task<bool>>? ShowConnectAction { get; set; }

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private string _connectionStatusText = "Not configured";

    [ObservableProperty]
    private bool _isXboxConnected;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsBrowseActive));
        OnPropertyChanged(nameof(IsInstalledActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        UpdateActiveView();
    }

    public bool IsBrowseActive => SelectedTab == 0;
    public bool IsInstalledActive => SelectedTab == 1;
    public bool IsSettingsActive => SelectedTab == 2;

    public int ActiveViewIndex
    {
        get => SelectedTab;
        set
        {
            if (value >= 0 && value <= 2)
                SelectedTab = value;
        }
    }

    public void UpdateConnectionStatus()
    {
        IsXboxConnected = _xboxService.IsConfigured;
        ConnectionStatusText = _xboxService.IsConfigured ? "Connected" : "Not configured";
    }

    private void UpdateActiveView()
    {
        OnPropertyChanged(nameof(ActiveViewIndex));
    }

    [RelayCommand]
    private void OpenAbout()
    {
        ShowAboutAction?.Invoke();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (ShowConnectAction is null) return;

        var result = await ShowConnectAction();

        if (result)
        {
            IsXboxConnected = true;
            ConnectionStatusText = "Connected";
        }
    }
}
