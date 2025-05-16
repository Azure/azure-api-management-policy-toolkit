// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class CacheStore
{
    private readonly Dictionary<string, CacheValue> _internalCache = new();
    private readonly Dictionary<string, CacheValue> _externalCache = new();

    private bool _isExternalCacheSetup = false;

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

    public CacheStore WithExternalCacheSetup(bool isSetup = true)
    {
        _isExternalCacheSetup = isSetup;
        return this;
    }

    public CacheStore WithExternalCacheValue(string key, object value, uint duration = 10)
    {
        _externalCache.Add(key, new CacheValue(value, duration));
        return this;
    }

    public CacheStore WithInternalCacheValue(string key, object value, uint duration = 10)
    {
        _internalCache.Add(key, new CacheValue(value, duration));
        return this;
    }
}