using System;
using System.Collections.Concurrent;

namespace XBVault.Services;

/// <summary>
/// Very small service locator used to bootstrap DI incrementally.
/// Only intended as a minimal compatibility layer during migration.
/// Not for public API. Thread-safe.
/// </summary>
internal static class ServiceLocator
{
    private static readonly ConcurrentDictionary<Type, object> _services = new();

    public static void Register<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance!;
    }

    public static T Resolve<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var inst) && inst is T t)
            return t;

        // Fallback: auto-register a SerilogAdapter for IAppLogger to keep tests and legacy
        // consumers working while we migrate DI. This keeps changes minimal.
        if (typeof(T) == typeof(IAppLogger))
        {
            var a = new SerilogAdapter();
            _services[typeof(T)] = a;
            return a as T ?? throw new InvalidOperationException($"Failed to create adapter for {typeof(T).FullName}");
        }

        throw new InvalidOperationException($"Service of type {typeof(T).FullName} not registered");
    }
}
