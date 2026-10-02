// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal sealed class CacheStoreService(
    Dictionary<string, CacheValue> entries,
    Func<TimeProvider> timeProvider) : ICache
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();
    private readonly AsyncLocal<HashSet<string>?> _activeFactories = new();

    internal TimeProvider Clock => timeProvider();

    public Task<object?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ReadEntry(key)?.Value);
    }

    public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        CachePolicyServices.ValidateTtl(ttl, ttl);
        ct.ThrowIfCancellationRequested();
        WriteEntry(key, value, ttl, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();
        lock (entries)
        {
            entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task<CacheValueResult> GetOrCreateAsync(
        string key,
        TimeSpan expiresAfter,
        TimeSpan? refreshAfter,
        Func<object?, CancellationToken, Task<object?>> valueFactory,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        var refresh = refreshAfter ?? expiresAfter;
        CachePolicyServices.ValidateTtl(expiresAfter, refresh);
        return GetOrCreateWithDynamicTtlAsync(key, async (previous, cancellation) =>
        {
            var value = await valueFactory(previous, cancellation).ConfigureAwait(false);
            return value is null
                ? CacheValueFactoryResult.DoNotUpdate()
                : new CacheValueFactoryResult(value, expiresAfter, refresh);
        }, forceRefresh: refresh == TimeSpan.Zero, ct);
    }

    public async Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
        string key,
        Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);
        ct.ThrowIfCancellationRequested();
        if (_activeFactories.Value?.Contains(key) == true)
        {
            throw new InvalidOperationException($"A cache value factory cannot recursively refresh its own key '{key}'.");
        }

        var cached = ReadEntry(key);
        if (!forceRefresh && cached is not null && !NeedsRefresh(cached))
        {
            return new CacheValueResult(cached.Value, wasRefreshed: false, wasCacheMiss: false);
        }

        var refreshLock = _refreshLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(ct).ConfigureAwait(false);
        var previousFactories = _activeFactories.Value;
        try
        {
            ct.ThrowIfCancellationRequested();
            // Another request may have filled or refreshed this key while we waited.
            cached = ReadEntry(key);
            if (!forceRefresh && cached is not null && !NeedsRefresh(cached))
            {
                return new CacheValueResult(cached.Value, wasRefreshed: false, wasCacheMiss: false);
            }

            _activeFactories.Value = new HashSet<string>(previousFactories ?? [], StringComparer.Ordinal) { key };
            var wasCacheMiss = cached is null;
            var result = await valueFactory(cached?.Value, ct).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(result);
            ct.ThrowIfCancellationRequested();
            if (result.ShouldUpdateCache)
            {
                CachePolicyServices.ValidateTtl(result.ExpiresAfter, result.RefreshAfter);
                if (result.Value is not null)
                {
                    WriteEntry(key, result.Value, result.ExpiresAfter, result.RefreshAfter);
                    return new CacheValueResult(result.Value, wasRefreshed: !wasCacheMiss, wasCacheMiss);
                }
            }

            return new CacheValueResult(ReadEntry(key)?.Value, wasRefreshed: false, wasCacheMiss);
        }
        finally
        {
            _activeFactories.Value = previousFactories;
            refreshLock.Release();
        }
    }

    private CacheValue? ReadEntry(string key)
    {
        lock (entries)
        {
            if (!entries.TryGetValue(key, out var cached))
            {
                return null;
            }
            if (!cached.IsExpiredAt(timeProvider().GetUtcNow().UtcDateTime))
            {
                return cached;
            }

            entries.Remove(key);
            return null;
        }
    }

    private bool NeedsRefresh(CacheValue cached) =>
        cached.NeedsRefreshAt(timeProvider().GetUtcNow().UtcDateTime);

    private void WriteEntry(string key, object value, TimeSpan ttl, TimeSpan refreshAfter)
    {
        var entry = new CacheValue(value, ttl, refreshAfter, timeProvider());
        lock (entries)
        {
            entries[key] = entry;
        }
    }
}