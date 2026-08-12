using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XBVault.Models;
using XBVault.Services;

namespace XBVault.ViewModels;

public partial class TaskCenterViewModel : ObservableObject
{
    private readonly BackgroundTaskService _service;

    public TaskCenterViewModel(BackgroundTaskService service)
    {
        _service = service;
        service.TaskAdded += OnTaskActivityChanged;
        service.TaskRemoved += OnTaskActivityChanged;
        ActiveCount = service.ActiveCount;
    }

    public ObservableCollection<BackgroundTask> Running => _service.ActiveTasks;
    public ObservableCollection<BackgroundTask> Scheduled => _service.ScheduledJobs;
    public ObservableCollection<BackgroundTask> Recent => _service.RecentTasks;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private int _activeCount;

    public void Cancel(BackgroundTask task) => _service.Cancel(task.Id);

    public bool RunNow(BackgroundTask task)
    {
        if (string.IsNullOrEmpty(task.JobKey))
            return false;
        return _service.RunJobNow(task.JobKey);
    }

    [RelayCommand]
    private void Toggle()
    {
        Logger.Trace($"Flyout: TaskCenter.Toggle {IsOpen} -> {!IsOpen}");
        IsOpen = !IsOpen;
    }

    private void OnTaskActivityChanged(object? sender, BackgroundTask e) => ActiveCount = _service.ActiveCount;
}
