// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

/// <summary>
/// Service registry for injecting custom service implementations into the gateway emulator.
/// Supports type-based and keyed service registration for mocking external dependencies.
/// </summary>
public class ServiceRegistry
{
    private readonly ConcurrentDictionary<(Type, string?), object> _services = new();

    /// <summary>
    /// Registers a service instance for the specified type.
    /// </summary>
    public ServiceRegistry Register<TService>(TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _services[(typeof(TService), null)] = instance;
        return this;
    }

    /// <summary>
    /// Registers a keyed service instance for the specified type.
    /// </summary>
    public ServiceRegistry Register<TService>(string key, TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(instance);
        _services[(typeof(TService), key)] = instance;
        return this;
    }

    /// <summary>
    /// Resolves a service by type. Returns null if not registered.
    /// </summary>
    public TService? Resolve<TService>(string? key = null) where TService : class
    {
        return _services.TryGetValue((typeof(TService), key), out var service)
            ? (TService)service
            : null;
    }

    /// <summary>
    /// Returns true if a service of the specified type is registered.
    /// </summary>
    public bool HasService<TService>(string? key = null) where TService : class
    {
        return _services.ContainsKey((typeof(TService), key));
    }

    /// <summary>
    /// Copies all registered services to another ServiceRegistry.
    /// Existing registrations in the target are NOT overwritten.
    /// </summary>
    public void CopyTo(ServiceRegistry target)
    {
        foreach (var kvp in _services)
        {
            target._services.TryAdd(kvp.Key, kvp.Value);
        }
    }
}
