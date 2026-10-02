// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record CacheValue
{
    private readonly TimeProvider _timeProvider;

    public object Value { get; init; }
    public DateTime StoredAt { get; init; }
    public TimeSpan Ttl { get; init; }
    public TimeSpan RefreshAfter { get; init; }
    public DateTime ExpiresAt => StoredAt + Ttl;
    public DateTime RefreshAt => StoredAt + RefreshAfter;
    public bool IsExpired => IsExpiredAt(_timeProvider.GetUtcNow().UtcDateTime);
    public bool NeedsRefresh => NeedsRefreshAt(_timeProvider.GetUtcNow().UtcDateTime);

    public CacheValue(object value, int duration = 0)
        : this(value, TimeSpan.FromSeconds(duration), TimeSpan.FromSeconds(duration))
    {
    }

    public CacheValue(object value, TimeSpan ttl, TimeSpan refreshAfter)
        : this(value, ttl, refreshAfter, TimeProvider.System)
    {
    }

    public CacheValue(object value, TimeSpan ttl, TimeSpan refreshAfter, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        Value = value;
        StoredAt = timeProvider.GetUtcNow().UtcDateTime;
        Ttl = ttl;
        RefreshAfter = refreshAfter;
    }

    internal bool IsExpiredAt(DateTime utcNow) => utcNow >= ExpiresAt;

    internal bool NeedsRefreshAt(DateTime utcNow) => utcNow >= RefreshAt;
}