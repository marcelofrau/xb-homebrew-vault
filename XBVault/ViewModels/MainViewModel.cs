using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    private static readonly string[] TabNames = ["Browse", "Installed", "FileExplorer", "Tools", "Settings", "Logs"];

    public MainViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
        Logger.Debug("MainViewModel initialized");
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
        var tabName = value >= 0 && value < TabNames.Length ? TabNames[value] : $"?{value}";
        Logger.Info($"Tab switched to {tabName} ({value})");
        Logger.Trace($"Previous: {_selectedTab}, New: {value}");
        OnPropertyChanged(nameof(IsBrowseActive));
        OnPropertyChanged(nameof(IsInstalledActive));
        OnPropertyChanged(nameof(IsFileExplorerActive));
        OnPropertyChanged(nameof(IsToolsActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsLogsActive));
        UpdateActiveView();
    }

    public bool IsBrowseActive => SelectedTab == 0;
    public bool IsInstalledActive => SelectedTab == 1;
    public bool IsFileExplorerActive => SelectedTab == 2;
    public bool IsToolsActive => SelectedTab == 3;
    public bool IsSettingsActive => SelectedTab == 4;
    public bool IsLogsActive => SelectedTab == 5;

    public int ActiveViewIndex
    {
        get => SelectedTab;
        set
        {
            if (value >= 0 && value <= 5)
                SelectedTab = value;
        }
    }

    public void UpdateConnectionStatus()
    {
        IsXboxConnected = _xboxService.IsConfigured;
        ConnectionStatusText = _xboxService.IsConfigured ? "Connected" : "Not configured";
        Logger.Debug($"Connection status updated: {ConnectionStatusText}");
    }

    private void UpdateActiveView()
    {
        OnPropertyChanged(nameof(ActiveViewIndex));
    }

    [RelayCommand]
    private void OpenAbout()
    {
        Logger.Info("About dialog opened");
        ShowAboutAction?.Invoke();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        Logger.Info("Connect button clicked — opening connection dialog");
        if (ShowConnectAction is null)
        {
            Logger.Warn("ShowConnectAction not set, cannot connect");
            return;
        }

        var result = await ShowConnectAction();

        if (result)
        {
            IsXboxConnected = true;
            ConnectionStatusText = "Connected";
            Logger.Info("Xbox connection established from MainViewModel");
        }
        else
        {
            Logger.Info("Xbox connection failed or cancelled");
        }
    }
}
