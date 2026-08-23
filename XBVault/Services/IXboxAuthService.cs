#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

/// <summary>
/// Owns Xbox Dev Portal connection state, credentials, and shared transport metadata.
/// </summary>
/// <remarks>
/// Frontends should depend on this interface instead of constructing HTTP or SSH credentials directly.
/// The desktop and Android shells can share the same implementation because all Xbox communication is HTTP/SSH.
/// </remarks>
public interface IXboxAuthService : IDisposable
{
    /// <summary>
    /// Raised whenever the logical Xbox connection state changes.
    /// </summary>
    event Action<bool>? ConnectionChanged;

    /// <summary>
    /// Gets whether the service has enough settings to attempt a connection.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Gets whether the last connection attempt succeeded and the service is considered online.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the SMB password returned by the Xbox Device Portal, when available.
    /// </summary>
    string? SmbPassword { get; }

    /// <summary>
    /// Gets the configured Xbox host name or IP address without scheme or port.
    /// </summary>
    string? Host { get; }

    /// <summary>
    /// Configures the Xbox Device Portal endpoint and credentials.
    /// </summary>
    void Configure(string baseUrl, string username, string password);

    /// <summary>
    /// Returns SSH/SFTP credentials derived from the configured Xbox connection.
    /// </summary>
    SshConnectionInfo GetSshCredentials();

    /// <summary>
    /// Fetches and caches the SMB password exposed by the Xbox Device Portal.
    /// </summary>
    Task<string?> FetchSmbPasswordAsync();

    /// <summary>
    /// Returns the configured Xbox Device Portal URL suitable for opening in a browser.
    /// </summary>
    string? GetDevPortalUrl();

    /// <summary>
    /// Marks the service as connected without issuing a network request.
    /// </summary>
    void MarkConnected();

    /// <summary>
    /// Clears logical connection state and notifies listeners.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Ensures the Xbox is reachable, testing the connection when needed.
    /// </summary>
    Task<bool> EnsureConnectedAsync(CancellationToken ct = default);

    /// <summary>
    /// Performs a direct connection test against the Xbox Device Portal.
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);
}
