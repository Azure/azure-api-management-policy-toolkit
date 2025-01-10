// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class CacheStore
{
    private readonly Dictionary<string, object> _internalCache = new();
    private readonly Dictionary<string, object> _externalCache = new();

    private bool _isExternalCacheSetup = false;

    internal Dictionary<string, object>? GetCache(string type) =>
        type switch
        {
            "internal" => _internalCache,
            "external" => _isExternalCacheSetup ? _externalCache : null,
            "prefer-external" => _isExternalCacheSetup ? _externalCache : _internalCache,
            _ => throw new ArgumentException($"Unrecognized type {type}", nameof(type)),
        };

    public IReadOnlyDictionary<string, object> InternalCache => _internalCache;
    public IReadOnlyDictionary<string, object> ExternalCache => _externalCache;

    public CacheStore WithExternalCacheSetup(bool isSetup = true)
    {
        _isExternalCacheSetup = isSetup;
        return this;
    }

    public CacheStore WithExternalCacheValue(string key, object value)
    {
        _externalCache.Add(key, value);
        return this;
    }

    public CacheStore WithInternalCacheValue(string key, object value)
    {
        _internalCache.Add(key, value);
        return this;
    }
}