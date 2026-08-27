#nullable enable
using System;
using System.Threading.Tasks;
using XBVault.Services;

namespace XBVault.Helpers;

public static class TaskExtensions
{
    /// <summary>
    /// Runs a task in fire-and-forget manner and logs any exception via Logger.
    /// Use for event handlers where async void would otherwise hide exceptions.
    /// </summary>
    public static void FireAndForget(this Task task, string? context = null)
    {
        if (task is null) return;

        // Observe exceptions and log on UI dispatcher to avoid finalizer-thread rethrows
        task.ContinueWith(t =>
        {
            var ex = t.Exception?.Flatten().InnerException;
            if (ex is not null)
            {
                // Use centralized UI helper to post to UI thread safely
                try
                {
                    XBVault.Helpers.UIHelpers.RunOnUI(() => Logger.Error(ex, context ?? "FireAndForget"));
                }
                catch
                {
                    Logger.Error(ex, context ?? "FireAndForget");
                }
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
