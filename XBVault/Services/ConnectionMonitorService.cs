using System;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public sealed class ConnectionMonitorService : IDisposable
{
    private readonly IXboxAuthService _authService;
    private readonly NotificationCenterService _notifications;
    private readonly BackgroundTaskService _taskService;
    private volatile bool _knownAlive;
    private volatile bool _stopped;

    public event EventHandler<ConnectionLostEventArgs>? ConnectionLost;
    public event EventHandler? ConnectionRestored;

    public ConnectionMonitorService(IXboxAuthService authService, NotificationCenterService notifications, BackgroundTaskService taskService)
    {
        _authService = authService;
        _notifications = notifications;
        _taskService = taskService;
        _knownAlive = authService.IsConnected;
        authService.ConnectionChanged += OnConnectionChanged;
    }

    public void Start()
    {
        _taskService.RegisterJob(
            "Connection Monitor",
            ReadCheckInterval,
            CheckAsync,
            canCancel: false);
        Logger.Debug("ConnectionMonitorService started");
    }

    public void Stop() => _stopped = true;

    private void OnConnectionChanged(bool connected)
    {
        _knownAlive = connected;
    }

    private static TimeSpan ReadCheckInterval()
    {
        var seconds = SettingsService.Current.ConnectionCheckIntervalSeconds;
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task CheckAsync(BackgroundTask task, CancellationToken ct)
    {
        if (_stopped || !_authService.IsConnected)
            return;

        var result = await _authService.TestConnectionAsync(ct);
        if (ct.IsCancellationRequested)
            return;

        if (result.Success)
        {
            if (!_knownAlive)
            {
                _knownAlive = true;
                ConnectionRestored?.Invoke(this, EventArgs.Empty);
                _notifications.Notify("Connection Restored", "Connection to the Xbox has been re-established.");
            }
        }
        else if (!result.IsCancelled)
        {
            if (_knownAlive)
            {
                _knownAlive = false;
                var reason = result.ErrorDetail ?? "Connection lost";
                ConnectionLost?.Invoke(this, new ConnectionLostEventArgs(reason));
                _notifications.Notify("Connection Lost", reason);
            }
        }
    }

    public void Dispose()
    {
        _authService.ConnectionChanged -= OnConnectionChanged;
    }
}

public sealed class ConnectionLostEventArgs(string reason) : EventArgs
{
    public string Reason { get; } = reason;
}
