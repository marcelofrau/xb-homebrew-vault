using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using XBVault.Models;

namespace XBVault.Services;

public class BackgroundTaskService
{
    public const int MaxRecentTasks = 50;
    public static readonly TimeSpan ElapsedTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan JobDisabledPollInterval = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private readonly Dictionary<string, JobEntry> _jobs = new(StringComparer.Ordinal);
    private int _runningCount;
    private DispatcherTimer? _elapsedTimer;
    private bool _started;

    public ObservableCollection<BackgroundTask> ActiveTasks { get; } = [];
    public ObservableCollection<BackgroundTask> ScheduledJobs { get; } = [];
    public ObservableCollection<BackgroundTask> RecentTasks { get; } = [];

    public event EventHandler<BackgroundTask>? TaskAdded;
    public event EventHandler<BackgroundTask>? TaskRemoved;
    public event EventHandler<BackgroundTask>? TaskChanged;

    public int ActiveCount => ActiveTasks.Count;

    public bool IsRunning => _started;

    public void Start()
    {
        _started = true;
        Logger.Debug("BackgroundTaskService started");
    }

    public BackgroundTask RunAsync(string name, Func<BackgroundTask, CancellationToken, Task> work, bool canCancel = true)
    {
        EnsureStarted();
        var task = CreateTask(name, BackgroundTaskKind.OneShot, canCancel, jobKey: null);
        PostToUi(() =>
        {
            ActiveTasks.Add(task);
            TaskAdded?.Invoke(this, task);
        });
        _ = RunCoreAsync(task, work);
        return task;
    }

    public void RegisterJob(string name, Func<TimeSpan> intervalProvider, Func<BackgroundTask, CancellationToken, Task> work, bool canCancel = true)
    {
        EnsureStarted();
        var scheduled = new BackgroundTask(name, BackgroundTaskKind.Job, canCancel, name, Marshal);
        var entry = new JobEntry
        {
            Key = name,
            IntervalProvider = intervalProvider,
            Work = work,
            CanCancel = canCancel,
            ScheduledTask = scheduled
        };
        BackgroundTask? replaced = null;
        lock (_gate)
        {
            if (_jobs.TryGetValue(name, out var existing))
            {
                existing.StopSource.Cancel();
                replaced = existing.ScheduledTask;
            }
            _jobs[name] = entry;
        }
        if (replaced is not null)
            RemoveScheduled(replaced);
        PostToUi(() => ScheduledJobs.Add(scheduled));
        _ = RunJobLoopAsync(entry);
    }

    private void RemoveScheduled(BackgroundTask scheduled)
    {
        PostToUi(() => ScheduledJobs.Remove(scheduled));
    }

    public bool Cancel(Guid taskId)
    {
        lock (_gate)
        {
            if (!_cancellations.TryGetValue(taskId, out var cts))
                return false;
            cts.Cancel();
            return true;
        }
    }

    public bool RunJobNow(string jobName)
    {
        EnsureStarted();
        JobEntry entry;
        lock (_gate)
        {
            if (!_jobs.TryGetValue(jobName, out var found))
                return false;
            entry = found;
        }

        Logger.Info($"BackgroundTaskService: running job '{jobName}' now (manual trigger)");
        var task = CreateTask(entry.Key, BackgroundTaskKind.Job, entry.CanCancel, entry.Key);
        PostToUi(() =>
        {
            ActiveTasks.Add(task);
            TaskAdded?.Invoke(this, task);
        });
        _ = RunCoreAsync(task, entry.Work);
        return true;
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;

        JobEntry[] jobs;
        lock (_gate)
        {
            jobs = [.. _jobs.Values];
            _jobs.Clear();
        }
        foreach (var job in jobs)
            job.StopSource.Cancel();

        CancellationTokenSource[] cancellations;
        lock (_gate)
        {
            cancellations = [.. _cancellations.Values];
            _cancellations.Clear();
        }
        foreach (var cts in cancellations)
            cts.Cancel();

        PostToUi(() =>
        {
            ScheduledJobs.Clear();
            StopElapsedTicker();
        });
        Logger.Debug("BackgroundTaskService stopped");
    }

    private void EnsureStarted()
    {
        if (!_started)
            throw new InvalidOperationException("BackgroundTaskService is not started. Call Start() before running tasks or registering jobs.");
    }

    private BackgroundTask CreateTask(string name, BackgroundTaskKind kind, bool canCancel, string? jobKey)
    {
        var task = new BackgroundTask(name, kind, canCancel, jobKey, Marshal);
        task.Mutated += _ => TaskChanged?.Invoke(this, task);
        lock (_gate)
        {
            if (canCancel)
                _cancellations[task.Id] = new CancellationTokenSource();
        }
        return task;
    }

    private async Task RunCoreAsync(BackgroundTask task, Func<BackgroundTask, CancellationToken, Task> work)
    {
        CancellationToken token = default;
        lock (_gate)
        {
            if (_cancellations.TryGetValue(task.Id, out var cts))
                token = cts.Token;
        }

        task.SetRunning();
        AdjustRunningCount(1);

        try
        {
            await Task.Run(() => work(task, token));
            task.Complete(BackgroundTaskStatus.Succeeded, "Completed");
        }
        catch (OperationCanceledException)
        {
            task.Complete(BackgroundTaskStatus.Cancelled, "Cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"BackgroundTask '{task.Name}' failed");
            task.AppendDetail(ex.Message);
            task.Complete(BackgroundTaskStatus.Failed, "Failed");
        }
        finally
        {
            lock (_gate)
            {
                _cancellations.Remove(task.Id);
            }
            AdjustRunningCount(-1);
            RemoveActive(task);
        }
    }

    private async Task RunJobLoopAsync(JobEntry entry)
    {
        var token = entry.StopSource.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var interval = ReadInterval(entry);
                if (interval <= TimeSpan.Zero)
                {
                    entry.ScheduledTask.SetNextRun(null);
                    try
                    {
                        await Task.Delay(JobDisabledPollInterval, token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    continue;
                }

                entry.ScheduledTask.SetNextRun(DateTime.UtcNow.Add(interval));

                try
                {
                    using var timer = new PeriodicTimer(interval);
                    await timer.WaitForNextTickAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (token.IsCancellationRequested) return;

                var task = CreateTask(entry.Key, BackgroundTaskKind.Job, entry.CanCancel, entry.Key);
                PostToUi(() =>
                {
                    ActiveTasks.Add(task);
                    TaskAdded?.Invoke(this, task);
                });
                await RunCoreAsync(task, entry.Work);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_jobs.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry))
                    _jobs.Remove(entry.Key);
            }
        }
    }

    private static TimeSpan ReadInterval(JobEntry entry)
    {
        try
        {
            return entry.IntervalProvider();
        }
        catch (Exception ex)
        {
            Logger.Debug($"BackgroundTaskService: interval provider for job '{entry.Key}' threw: {ex.Message}");
            return TimeSpan.Zero;
        }
    }

    private void RemoveActive(BackgroundTask task)
    {
        PostToUi(() =>
        {
            ActiveTasks.Remove(task);
            var existing = RecentTasks.FirstOrDefault(t => t.Name == task.Name);
            if (existing is not null)
                RecentTasks.Remove(existing);
            RecentTasks.Insert(0, task);
            while (RecentTasks.Count > MaxRecentTasks)
                RecentTasks.RemoveAt(RecentTasks.Count - 1);
            TaskRemoved?.Invoke(this, task);
        });
    }

    private void AdjustRunningCount(int delta)
    {
        lock (_gate)
        {
            _runningCount = Math.Max(0, _runningCount + delta);
        }
        EnsureElapsedTicker();
    }

    private void EnsureElapsedTicker()
    {
        PostToUi(() =>
        {
            bool hasRunning;
            lock (_gate)
            {
                hasRunning = _runningCount > 0;
            }

            if (hasRunning)
            {
                if (_elapsedTimer is null)
                {
                    _elapsedTimer = CreateElapsedTimer();
                    if (_elapsedTimer is null) return;
                    _elapsedTimer.Tick += OnElapsedTick;
                    _elapsedTimer.Start();
                }
            }
            else
            {
                StopElapsedTicker();
            }
        });
    }

    private void StopElapsedTicker()
    {
        if (_elapsedTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnElapsedTick;
            _elapsedTimer = null;
        }
    }

    private void OnElapsedTick(object? sender, EventArgs e)
    {
        foreach (var task in ActiveTasks)
        {
            if (task.IsRunning)
                task.NotifyElapsedChanged();
        }
    }

    private void Marshal(Action action) => PostToUi(action);

    protected virtual DispatcherTimer? CreateElapsedTimer()
        => new() { Interval = ElapsedTickInterval };

    protected virtual void PostToUi(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            Logger.Debug($"BackgroundTaskService: UI dispatch failed: {ex.Message}");
        }
    }

    private sealed class JobEntry
    {
        public required string Key { get; init; }
        public required Func<TimeSpan> IntervalProvider { get; init; }
        public required Func<BackgroundTask, CancellationToken, Task> Work { get; init; }
        public bool CanCancel { get; init; }
        public required BackgroundTask ScheduledTask { get; init; }
        public CancellationTokenSource StopSource { get; } = new();
    }
}
