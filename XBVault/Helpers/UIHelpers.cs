#nullable enable
using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace XBVault.Helpers;

/// <summary>
/// Small helper to invoke actions on Avalonia UI thread safely.
/// Use RunOnUI for synchronous actions and RunOnUIAsync for async delegates.
/// </summary>
public static class UIHelpers
{
    public static void RunOnUI(Action action)
        => RunOnUI(action, DispatcherPriority.Normal);

    public static void RunOnUI(Action action, DispatcherPriority priority)
    {
        var d = Dispatcher.UIThread;
        if (d.CheckAccess())
            action();
        else
            d.Post(action, priority);
    }

    public static Task RunOnUIAsync(Func<Task> func)
        => RunOnUIAsync(func, DispatcherPriority.Normal);

    public static Task RunOnUIAsync(Func<Task> func, DispatcherPriority priority)
    {
        if (func is null) return Task.CompletedTask;
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnUI(async () =>
        {
            try
            {
                await func().ConfigureAwait(false);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, priority);
        return tcs.Task;
    }
}
