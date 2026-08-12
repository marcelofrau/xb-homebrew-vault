using Avalonia.Threading;
using XBVault.Models;

namespace XBVault.Tests;

public class BackgroundTaskServiceTests
{
    private sealed class InlineDispatcherTaskService : BackgroundTaskService
    {
        protected override void PostToUi(Action action) => action();
        protected override DispatcherTimer? CreateElapsedTimer() => null;
    }

    private sealed class CountingDispatcherTaskService : BackgroundTaskService
    {
        public int PostCount;

        protected override void PostToUi(Action action)
        {
            Interlocked.Increment(ref PostCount);
            action();
        }

        protected override DispatcherTimer? CreateElapsedTimer() => null;
    }

    [Fact]
    public void RunAsync_ThrowsWhenNotStarted()
    {
        var service = new InlineDispatcherTaskService();

        Assert.Throws<InvalidOperationException>(() =>
            service.RunAsync("x", (t, ct) => Task.CompletedTask));
    }

    [Fact]
    public async Task RunAsync_Succeeds_MovesToRecent()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var task = service.RunAsync("install", (t, ct) => Task.CompletedTask);

        await WaitUntil(() => task.IsFinished);

        Assert.Equal(BackgroundTaskStatus.Succeeded, task.Status);
        Assert.Equal("Completed", task.StatusMessage);
        Assert.Empty(service.ActiveTasks);
        Assert.Single(service.RecentTasks);
        Assert.Equal(0, service.ActiveCount);
    }

    [Fact]
    public async Task RunAsync_ReportsProgress()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var task = service.RunAsync("progress", (t, ct) =>
        {
            t.ReportProgress(0.25);
            t.ReportProgress(0.75);
            return Task.CompletedTask;
        });

        await WaitUntil(() => task.IsFinished);

        Assert.Equal(0.75, task.Progress);
    }

    [Fact]
    public async Task RunAsync_Cancel_MarksCancelled()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var task = service.RunAsync("cancel", async (t, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
        });

        await WaitUntil(() => task.IsRunning);
        Assert.True(service.Cancel(task.Id));

        await WaitUntil(() => task.IsFinished);

        Assert.Equal(BackgroundTaskStatus.Cancelled, task.Status);
        Assert.Empty(service.ActiveTasks);
    }

    [Fact]
    public async Task RunAsync_Failure_MarksFailedAndAppendsDetail()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var task = service.RunAsync("fail", (t, ct) => throw new InvalidOperationException("boom"));

        await WaitUntil(() => task.IsFinished);

        Assert.Equal(BackgroundTaskStatus.Failed, task.Status);
        Assert.Equal("Failed", task.StatusMessage);
        Assert.Contains(task.Details, d => d.Contains("boom"));
    }

    [Fact]
    public async Task RunAsync_Cancel_DoesNotCancelOtherTasks()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var victim = service.RunAsync("victim", async (t, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
        });
        var other = service.RunAsync("other", (t, ct) => Task.CompletedTask);

        await WaitUntil(() => victim.IsRunning);
        service.Cancel(victim.Id);

        await WaitUntil(() => victim.IsFinished);
        await WaitUntil(() => other.IsFinished);

        Assert.Equal(BackgroundTaskStatus.Cancelled, victim.Status);
        Assert.Equal(BackgroundTaskStatus.Succeeded, other.Status);
    }

    [Fact]
    public async Task RegisterJob_RunsRepeatedly_ExposesNextRun()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var runs = 0;
        service.RegisterJob("conn-monitor", () => TimeSpan.FromMilliseconds(15), (t, ct) =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        });

        await WaitUntil(() => runs >= 3);

        var scheduled = Assert.Single(service.ScheduledJobs);
        Assert.Equal("conn-monitor", scheduled.Name);
        Assert.NotNull(scheduled.NextRunAt);

        service.Stop();
        Assert.Empty(service.ScheduledJobs);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task RegisterJob_ZeroInterval_Disabled_NoNextRun()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var runs = 0;
        service.RegisterJob("disabled", () => TimeSpan.Zero, (t, ct) =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        });

        await Task.Delay(100);

        Assert.Equal(0, runs);
        var scheduled = Assert.Single(service.ScheduledJobs);
        Assert.Null(scheduled.NextRunAt);

        service.Stop();
    }

    [Fact]
    public async Task Stop_CancelsRunningJob()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var cancelled = false;
        service.RegisterJob("job", () => TimeSpan.FromMilliseconds(10), async (t, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
        });

        await WaitUntil(() => service.ActiveCount > 0);

        service.Stop();

        await WaitUntil(() => cancelled);
        Assert.Empty(service.ScheduledJobs);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task RegisterJob_ReplacesExisting_RemovesOldPlaceholder()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        service.RegisterJob("job", () => TimeSpan.FromMinutes(1), (t, ct) => Task.CompletedTask);

        await WaitUntil(() => service.ScheduledJobs.Count == 1);
        service.RegisterJob("job", () => TimeSpan.FromMinutes(1), (t, ct) => Task.CompletedTask);

        await WaitUntil(() => service.ScheduledJobs.Count == 1);
        service.Stop();
        Assert.Empty(service.ScheduledJobs);
    }

    [Fact]
    public async Task Elapsed_EqualsCompletedAtMinusCreatedAt()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var task = service.RunAsync("elapsed", (t, ct) => Task.CompletedTask);

        await WaitUntil(() => task.IsFinished);

        Assert.Equal(task.CompletedAt!.Value - task.CreatedAt, task.Elapsed);
    }

    [Fact]
    public async Task AllUiMutations_FlowThroughPostToUi()
    {
        var service = new CountingDispatcherTaskService();
        service.Start();
        var task = service.RunAsync("marshal", (t, ct) => Task.CompletedTask);

        await WaitUntil(() => task.IsFinished);

        Assert.True(service.PostCount >= 4, $"expected >=4 marshaled actions, got {service.PostCount}");
        Assert.Empty(service.ActiveTasks);
    }

    [Fact]
    public void RunJobNow_ThrowsWhenNotStarted()
    {
        var service = new InlineDispatcherTaskService();

        Assert.Throws<InvalidOperationException>(() => service.RunJobNow("conn-monitor"));
    }

    [Fact]
    public async Task RunJobNow_UnknownJob_ReturnsFalse()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();

        Assert.False(service.RunJobNow("does-not-exist"));
        service.Stop();
    }

    [Fact]
    public async Task RunJobNow_ExecutesWorkImmediately()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var runs = 0;
        service.RegisterJob("conn-monitor", () => TimeSpan.FromMinutes(30), (t, ct) =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        });

        await WaitUntil(() => service.ScheduledJobs.Count == 1);
        Assert.True(service.RunJobNow("conn-monitor"));

        await WaitUntil(() => runs == 1);
        service.Stop();
    }

    [Fact]
    public async Task RunJobNow_AppearsInActiveTasks_ThenMovesToRecent()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var started = new TaskCompletionSource();
        service.RegisterJob("job", () => TimeSpan.FromMinutes(30), async (t, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(20);
        });

        await WaitUntil(() => service.ScheduledJobs.Count == 1);
        Assert.True(service.RunJobNow("job"));

        await started.Task;
        Assert.Equal(BackgroundTaskStatus.Running, service.ActiveTasks.Single(t => t.JobKey == "job").Status);

        await WaitUntil(() => service.ActiveCount == 0);
        Assert.Single(service.RecentTasks);
        service.Stop();
    }

    [Fact]
    public async Task RunJobNow_DoesNotDisturbScheduledNextRun()
    {
        var service = new InlineDispatcherTaskService();
        service.Start();
        var runs = 0;
        service.RegisterJob("job", () => TimeSpan.FromMinutes(30), (t, ct) =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        });

        await WaitUntil(() => service.ScheduledJobs.Count == 1);
        var scheduledBefore = service.ScheduledJobs.Single().NextRunAt;

        Assert.True(service.RunJobNow("job"));
        await WaitUntil(() => runs == 1);

        Assert.Equal(scheduledBefore, service.ScheduledJobs.Single().NextRunAt);
        service.Stop();
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Condition not met within {timeoutMs} ms");
            await Task.Delay(10);
        }
    }
}
