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
        _xboxService.ConnectionChanged += OnConnectionChanged;
        Logger.Debug("MainViewModel initialized");
        UpdateConnectionStatus();
    }

    private void OnConnectionChanged(bool connected)
    {
        IsXboxConnected = connected;
        ConnectionStatusText = connected ? "Connected" : "Disconnected";
    }

    public Action? ShowAboutAction { get; set; }
    public Func<Task<bool>>? ShowConnectAction { get; set; }
    public Action? OnInstalledTabSelected { get; set; }

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private string _connectionStatusText = "Not configured";

    [ObservableProperty]
    private bool _isXboxConnected;

    [ObservableProperty]
    private bool _isConnecting;

    public bool IsNotConfigured => !IsXboxConnected && !SettingsService.Current.XboxConnection.IsConfigured;
    public bool IsDisconnected => !IsXboxConnected && SettingsService.Current.XboxConnection.IsConfigured;
    public bool CanConnect => !IsXboxConnected && !IsConnecting;
    public bool ShowConnecting => IsConnecting;

    partial void OnIsXboxConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotConfigured));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(ShowConnecting));
    }

    partial void OnIsConnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(ShowConnecting));
    }

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

        if (value == 1)
            OnInstalledTabSelected?.Invoke();
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
        if (_xboxService.IsConfigured)
        {
            IsXboxConnected = true;
            ConnectionStatusText = "Connected";
        }
        else if (SettingsService.Current.XboxConnection.IsConfigured)
        {
            IsXboxConnected = false;
            ConnectionStatusText = "Disconnected";
        }
        else
        {
            IsXboxConnected = false;
            ConnectionStatusText = "Not configured";
        }
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

        IsConnecting = true;

        var result = await ShowConnectAction();

        IsConnecting = false;

        if (result)
        {
            IsXboxConnected = true;
            _xboxService.MarkConnected();
            ConnectionStatusText = "Connected";
            Logger.Info("Xbox connection established from MainViewModel");
        }
        else
        {
            Logger.Info("Xbox connection failed or cancelled");
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        Logger.Info("Disconnect button clicked");
        _xboxService.Disconnect();
        UpdateConnectionStatus();
        Logger.Info("Xbox disconnected");
    }
}
