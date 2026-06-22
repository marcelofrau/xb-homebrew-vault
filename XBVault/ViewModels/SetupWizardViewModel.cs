using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class SetupWizardViewModel : ObservableObject
{
    private readonly XboxDeviceService _xboxService;

    public Action? CloseAction;

    public SetupWizardViewModel(XboxDeviceService xboxService)
    {
        _xboxService = xboxService;
    }

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string _port = "11443";

    [ObservableProperty]
    private bool _useHttps = true;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private bool _openConnectionAfter = true;

    [ObservableProperty]
    private string? _statusText;

    private bool _completed;
    public bool WasCompleted => _completed;

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsConsoleStep => CurrentStep == 1;
    public bool IsAuthStep => CurrentStep == 2;
    public bool IsReadyStep => CurrentStep == 3;

    public bool CanGoNext => CurrentStep switch
    {
        0 => true,
        1 => !string.IsNullOrWhiteSpace(Address),
        2 => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password),
        _ => false
    };

    public bool CanGoBack => CurrentStep > 0;
    public bool CanCancel => true;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsConsoleStep));
        OnPropertyChanged(nameof(IsAuthStep));
        OnPropertyChanged(nameof(IsReadyStep));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnAddressChanged(string? value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnUsernameChanged(string? value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnPasswordChanged(string? value)
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CurrentStep < 3)
            CurrentStep++;
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0)
            CurrentStep--;
    }

    [RelayCommand]
    private void Finish()
    {
        SaveToSettings();
        _completed = true;
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseAction?.Invoke();
    }

    private void SaveToSettings()
    {
        var settings = SettingsService.Current.XboxConnection;
        settings.Address = Address ?? "";
        settings.Port = int.TryParse(Port, out var p) ? p : 11443;
        settings.UseHttps = UseHttps;
        settings.Username = Username ?? "";
        settings.EncryptedPassword = CryptoService.Obfuscate(Password ?? "");
        SettingsService.Save();

        var baseUrl = settings.BaseUrl;
        _xboxService.Configure(baseUrl, settings.Username, Password ?? "");
    }
}
