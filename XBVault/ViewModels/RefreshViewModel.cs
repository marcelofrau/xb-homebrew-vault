using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class RefreshViewModel : ObservableObject
{
    private readonly EmulationRevivalService _erService;
    private readonly Func<Task>? _onCatalogRefreshed;

    public RefreshViewModel(EmulationRevivalService erService, Func<Task>? onCatalogRefreshed = null)
    {
        _erService = erService;
        _onCatalogRefreshed = onCatalogRefreshed;
        Logger.Debug("RefreshViewModel initialized");
    }

    public ObservableCollection<string> OutputLines { get; } = [];

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isRunning;

    public event Action<bool>? Completed;

    private void AddLine(string text)
    {
        OutputLines.Add(text);
        Logger.Info(text);
    }

    private async Task Delay(int ms) => await Task.Delay(ms);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        Progress = 0;
        OutputLines.Clear();
        Logger.Info("Refresh dialog started");

        AddLine("Initializing catalog refresh...");
        await Delay(200);
        Progress = 0.05;

        AddLine("Connecting to Emulation Revival...");
        await Delay(300);
        Progress = 0.1;

        AddLine("Downloading catalog pages...");
        Progress = 0.15;

        try
        {
            var progress = new Progress<(string Status, double Progress)>(p =>
            {
                Progress = 0.1 + p.Progress * 0.6;
                if (!string.IsNullOrEmpty(p.Status))
                    AddLine(p.Status);
            });

            AddLine("Fetching emulators, frontends, ports...");
            await Task.Delay(200);

            await _erService.FetchCatalogAsync(forceRefresh: true, progress: progress);

            Progress = 0.85;
            AddLine("");
            AddLine("Parsing and deduplicating...");
            await Delay(300);
            Progress = 0.9;

            AddLine("Caching results...");
            await Delay(200);
            Progress = 0.95;

            AddLine("");
            AddLine("Catalog refreshed successfully!");
            AddLine("New data available in Browse.");
            Progress = 1.0;
            await Delay(200);

            if (_onCatalogRefreshed is not null)
            {
                AddLine("Reloading catalog view...");
                await _onCatalogRefreshed();
            }

            AddLine("");
            AddLine("Done. Closing in 3 seconds...");
            await Delay(3000);

            Completed?.Invoke(true);
        }
        catch (Exception ex)
        {
            AddLine("");
            AddLine("ERROR: " + ex.Message);
            Logger.Error(ex, "Catalog refresh failed in dialog");
            await Delay(3000);
            Completed?.Invoke(false);
        }
        finally
        {
            IsRunning = false;
        }
    }
}
