// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record CacheValue
{
    public object Value { get; init; }
    public DateTime StoredAt { get; init; } = DateTime.UtcNow;
    public TimeSpan Ttl { get; init; }
    public TimeSpan RefreshAfter { get; init; }
    public DateTime ExpiresAt => StoredAt + Ttl;
    public DateTime RefreshAt => StoredAt + RefreshAfter;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool NeedsRefresh => DateTime.UtcNow >= RefreshAt;

    public CacheValue(object value, int duration = 0)
    {
        Value = value;
        Ttl = TimeSpan.FromSeconds(duration);
        RefreshAfter = Ttl;
    }

    public CacheValue(object value, TimeSpan ttl, TimeSpan refreshAfter)
    {
        Value = value;
        Ttl = ttl;
        RefreshAfter = refreshAfter;
    }
}