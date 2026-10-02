// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

/// <summary>
/// In-memory internal and external caches. Register a shared instance as <see cref="ICache"/>
/// to share cached values across test requests.
/// </summary>
public class CacheStore : ICache
{
    private readonly Dictionary<string, CacheValue> _internalCache = new();
    private readonly Dictionary<string, CacheValue> _externalCache = new();
    private readonly CacheStoreService _internalService;
    private readonly CacheStoreService _externalService;
    private TimeProvider _timeProvider;

    private bool _isExternalCacheSetup = false;

    public CacheStore() : this(TimeProvider.System)
    {
    }

    public CacheStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _internalService = new CacheStoreService(_internalCache, () => _timeProvider);
        _externalService = new CacheStoreService(_externalCache, () => _timeProvider);
    }

    internal Dictionary<string, CacheValue>? GetCache(string type) =>
        type switch
        {
            "internal" => _internalCache,
            "external" => _isExternalCacheSetup ? _externalCache : null,
            "prefer-external" => _isExternalCacheSetup ? _externalCache : _internalCache,
            _ => throw new ArgumentException($"Unrecognized type {type}", nameof(type)),
        };

    public IReadOnlyDictionary<string, CacheValue> InternalCache => _internalCache;
    public IReadOnlyDictionary<string, CacheValue> ExternalCache => _externalCache;

    internal TimeProvider Clock => _timeProvider;

    internal ICache? GetCacheService(string type)
    {
        var cache = GetCache(type);
        return cache is null ? null : ReferenceEquals(cache, _internalCache) ? _internalService : _externalService;
    }

    /// <summary>
    /// Sets the clock used for cache writes, expiration, and refresh decisions.
    /// Configure this before seeding entries for deterministic expiration tests.
    /// </summary>
    public CacheStore WithTimeProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        return this;
    }

    public CacheStore WithExternalCacheSetup(bool isSetup = true)
    {
        _isExternalCacheSetup = isSetup;
        return this;
    }

    public CacheStore WithExternalCacheValue(string key, object value, int duration = 10)
    {
        AddValue(_externalCache, key, value, duration);
        return this;
    }

    public CacheStore WithInternalCacheValue(string key, object value, int duration = 10)
    {
        AddValue(_internalCache, key, value, duration);
        return this;
    }

    public Task<object?> GetAsync(string key, CancellationToken ct = default) =>
        DefaultCache.GetAsync(key, ct);

    public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) =>
        DefaultCache.SetAsync(key, value, ttl, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        DefaultCache.RemoveAsync(key, ct);

    public Task<CacheValueResult> GetOrCreateAsync(
        string key,
        TimeSpan expiresAfter,
        TimeSpan? refreshAfter,
        Func<object?, CancellationToken, Task<object?>> valueFactory,
        CancellationToken ct = default) =>
        DefaultCache.GetOrCreateAsync(key, expiresAfter, refreshAfter, valueFactory, ct);

    public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
        string key,
        Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
        bool forceRefresh = false,
        CancellationToken ct = default) =>
        DefaultCache.GetOrCreateWithDynamicTtlAsync(key, valueFactory, forceRefresh, ct);

    private ICache DefaultCache => _isExternalCacheSetup ? _externalService : _internalService;

    private void AddValue(Dictionary<string, CacheValue> cache, string key, object value, int duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        var ttl = TimeSpan.FromSeconds(duration);
        lock (cache)
        {
            cache.Add(key, new CacheValue(value, ttl, ttl, _timeProvider));
        }
    }
}