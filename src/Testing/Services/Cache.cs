// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

public sealed class CachedResponse
{
    public int StatusCode { get; set; }

    public string? StatusReason { get; set; }

    public string? Body { get; set; }

    public Dictionary<string, string[]> Headers { get; set; } = new();
}

public sealed class CacheValueResult(object? value, bool wasRefreshed, bool wasCacheMiss)
{
    public object? Value { get; } = value;

    public bool WasRefreshed { get; } = wasRefreshed;

    public bool WasCacheMiss { get; } = wasCacheMiss;
}

public sealed class CacheValueFactoryResult
{
    public CacheValueFactoryResult(object? value, TimeSpan expiresAfter, TimeSpan refreshAfter)
    {
        Value = value;
        ShouldUpdateCache = true;
        ExpiresAfter = expiresAfter;
        RefreshAfter = refreshAfter;
    }

    public static CacheValueFactoryResult DoNotUpdate() => new();

    private CacheValueFactoryResult()
    {
        Value = null;
        ShouldUpdateCache = false;
        ExpiresAfter = TimeSpan.Zero;
        RefreshAfter = TimeSpan.Zero;
    }

    public object? Value { get; }

    public bool ShouldUpdateCache { get; }

    public TimeSpan ExpiresAfter { get; }

    public TimeSpan RefreshAfter { get; }
}

public interface ICache
{
    Task<object?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task<CacheValueResult> GetOrCreateAsync(
        string key,
        TimeSpan expiresAfter,
        TimeSpan? refreshAfter,
        Func<object?, CancellationToken, Task<object?>> valueFactory,
        CancellationToken ct = default);

    Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(
        string key,
        Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
        bool forceRefresh = false,
        CancellationToken ct = default);
}
