// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Admits nested policy execution without waiting. Share an instance through
/// <see cref="ServiceRegistry"/> to model concurrent requests across gateway contexts.
/// Register it as IConcurrencyLimiter, rather than its concrete implementation type.
/// </summary>
public interface IConcurrencyLimiter
{
    /// <summary>
    /// Returns a disposable permit, or null when the key has reached its limit.
    /// </summary>
    IDisposable? TryAcquire(string key, int maxCount);
}

/// <summary>
/// Thread-safe keyed concurrency limits with immediately rejected excess requests.
/// </summary>
public sealed class KeyedConcurrencyLimiter : IConcurrencyLimiter
{
    private readonly object _sync = new();
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IDisposable? TryAcquire(string key, int maxCount)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
        lock (_sync)
        {
            _counts.TryGetValue(key, out var count);
            if (count >= maxCount)
            {
                return null;
            }

            _counts[key] = count + 1;
            return new Permit(this, key);
        }
    }

    /// <summary>
    /// Returns the number of currently held permits for the key.
    /// </summary>
    public int GetCount(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            return _counts.TryGetValue(key, out var count) ? count : 0;
        }
    }

    private void Release(string key)
    {
        lock (_sync)
        {
            if (!_counts.TryGetValue(key, out var count) || count <= 0)
            {
                throw new InvalidOperationException($"Concurrency key '{key}' has no permit to release.");
            }

            if (count == 1)
            {
                _counts.Remove(key);
            }
            else
            {
                _counts[key] = count - 1;
            }
        }
    }

    private sealed class Permit(KeyedConcurrencyLimiter owner, string key) : IDisposable
    {
        private KeyedConcurrencyLimiter? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(key);
    }
}