using System;
using System.Threading;
using System.Threading.Tasks;
using XBVault.Models;

namespace XBVault.Services;

public interface IXboxAuthService : IDisposable
{
    event Action<bool>? ConnectionChanged;

    bool IsConfigured { get; }
    bool IsConnected { get; }
    string? SmbPassword { get; }
    string? Host { get; }

    void Configure(string baseUrl, string username, string password);
    SshConnectionInfo GetSshCredentials();
    Task<string?> FetchSmbPasswordAsync();
    string? GetDevPortalUrl();
    void MarkConnected();
    void Disconnect();
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);
}
