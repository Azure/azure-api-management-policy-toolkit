// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class RateLimitStore
{
    private readonly Dictionary<string, int> _counters = new();

    internal int Increment(string key, int amount = 1)
    {
        _counters.TryGetValue(key, out var current);
        var newCount = current + amount;
        _counters[key] = newCount;
        return newCount;
    }

    public int GetCount(string key) =>
        _counters.TryGetValue(key, out var count) ? count : 0;

    public RateLimitStore SetCount(string key, int count)
    {
        _counters[key] = count;
        return this;
    }

    public RateLimitStore Reset()
    {
        _counters.Clear();
        return this;
    }

    public RateLimitStore Reset(string key)
    {
        _counters.Remove(key);
        return this;
    }
}
