// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Request-local provenance for generated response headers that must survive backend replacement.
/// Live overrides and removals are reconciled without retaining arbitrary mocked response headers.
/// Deferred values use the forwarded headers; suppressed deferred outputs retain only the
/// provenance needed for a later explicit setter, until reconfiguration or a new RequestId.
/// Successful outer request boundaries refresh deferred values after all response processing
/// and limiter settlement, without taking ownership of terminal or replacement responses.
/// Provenance is bound to the response object. Forwarding callbacks explicitly retire deferred
/// output before they execute; copied header names cannot transfer response ownership.
/// </summary>
internal sealed class PolicyResponseHeaderOverlay
{
    private readonly GatewayContext _context;
    private readonly object _sync = new();
    private readonly Dictionary<string, HeaderOutput> _headers = new(StringComparer.OrdinalIgnoreCase);
    private Guid _requestId;
    private MockResponse _response;

    private PolicyResponseHeaderOverlay(GatewayContext context)
    {
        _context = context;
        _requestId = context.RequestId;
        _response = context.Response;
    }

    internal static PolicyResponseHeaderOverlay? Existing(GatewayContext context)
    {
        lock (context.Services)
        {
            var overlay = context.Services.Resolve<PolicyResponseHeaderOverlay>();
            if (overlay is not null && !ReferenceEquals(overlay._context, context))
            {
                throw new InvalidOperationException("PolicyResponseHeaderOverlay belongs to another gateway context.");
            }

            return overlay;
        }
    }

    private static PolicyResponseHeaderOverlay For(GatewayContext context)
    {
        lock (context.Services)
        {
            var overlay = Existing(context);
            if (overlay is null)
            {
                overlay = new PolicyResponseHeaderOverlay(context);
                context.Services.Register(overlay);
            }

            return overlay;
        }
    }

    internal static void SetHeader(GatewayContext context, string name, string[] values, Type source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(source);
        var overlay = For(context);
        lock (overlay._sync)
        {
            overlay.EnsureRequest();
            ResponseHeaderUtilities.RemoveCaseVariants(context.Response.Headers, name);
            context.Response.Headers[name] = values.ToArray();
            overlay._headers[name] = new HeaderOutput(values.ToArray(), source);
        }
    }

    internal static void SetNumericHeader(GatewayContext context, string? name, long value, Type source)
    {
        if (name is null)
        {
            return;
        }

        var overlay = For(context);
        lock (overlay._sync)
        {
            overlay.EnsureRequest();
            ResponseHeaderUtilities.SetNumericHeader(context.Response.Headers, name, value);
            overlay._headers[name] = new HeaderOutput(context.Response.Headers[name].ToArray(), source);
        }
    }

    internal static void SetDeferredHeader(
        GatewayContext context,
        string name,
        string[] initialValues,
        Type source,
        Func<IReadOnlyDictionary<string, string[]>, string[]> deferredValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialValues);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(deferredValue);
        var overlay = For(context);
        lock (overlay._sync)
        {
            overlay.EnsureRequest();
            ResponseHeaderUtilities.RemoveCaseVariants(context.Response.Headers, name);
            if (initialValues.Length > 0)
            {
                context.Response.Headers[name] = initialValues.ToArray();
            }

            overlay._headers[name] = new HeaderOutput(initialValues.ToArray(), source, deferredValue,
                IsPresent: initialValues.Length > 0);
        }
    }

    internal static void RemoveGeneratedHeaders(GatewayContext context, Type source)
    {
        var overlay = Existing(context);
        if (overlay is null)
        {
            return;
        }

        lock (overlay._sync)
        {
            overlay.EnsureRequest();
            overlay.Reconcile();
            foreach (var name in overlay._headers.Where(header => header.Value.Source == source)
                         .Select(header => header.Key).ToArray())
            {
                ResponseHeaderUtilities.RemoveCaseVariants(context.Response.Headers, name);
                overlay._headers.Remove(name);
            }
        }
    }

    internal static void ForgetHeader(GatewayContext context, string name)
    {
        var overlay = Existing(context);
        if (overlay is null)
        {
            return;
        }

        lock (overlay._sync)
        {
            overlay.EnsureRequest();
            if (overlay._headers.TryGetValue(name, out var output) && output.DeferredValue is not null)
            {
                overlay._headers[name] = output with { Values = [], IsPresent = false, IsSuppressed = true };
            }
            else
            {
                overlay._headers.Remove(name);
            }
        }
    }

    internal void UpdateRegistered(string name, string[] values)
    {
        lock (_sync)
        {
            EnsureRequest();
            if (_headers.ContainsKey(name))
            {
                _headers[name] = new HeaderOutput(values.ToArray(), Source: null);
            }
        }
    }

    internal Snapshot? Capture()
    {
        lock (_sync)
        {
            EnsureRequest();
            Reconcile();
            return _headers.Count == 0 ? null : new Snapshot(_requestId, _response,
                _headers.Select(header =>
                    new KeyValuePair<string, HeaderOutput>(header.Key,
                        header.Value with { Values = header.Value.Values.ToArray() })).ToArray());
        }
    }

    internal void Apply(Snapshot snapshot)
    {
        lock (_sync)
        {
            EnsureRequest();
            if (snapshot.RequestId != _requestId)
            {
                throw new InvalidOperationException("Cannot apply policy response headers from another request.");
            }
            if (!ReferenceEquals(snapshot.Response, _response))
            {
                throw new InvalidOperationException("Cannot apply policy response headers to another response.");
            }

            foreach (var (name, output) in snapshot.Headers)
            {
                ResponseHeaderUtilities.RemoveCaseVariants(_context.Response.Headers, name);
                if (output.DeferredValue is null)
                {
                    _context.Response.Headers[name] = output.Values.ToArray();
                }
            }

            FinalizeDeferredHeadersCore();
        }
    }

    internal void FinalizeDeferredHeaders()
    {
        lock (_sync)
        {
            EnsureRequest();
            Reconcile();
            FinalizeDeferredHeadersCore();
        }
    }

    internal void FinalizeResponse(GatewayContext context)
    {
        if (!ReferenceEquals(_context, context))
        {
            throw new InvalidOperationException("PolicyResponseHeaderOverlay belongs to another gateway context.");
        }

        lock (_sync)
        {
            EnsureRequest();
            if (context.ResponseTerminated)
            {
                return;
            }

            Reconcile();
            FinalizeDeferredHeadersCore();
        }
    }

    internal void RetireDeferredResponse()
    {
        lock (_sync)
        {
            EnsureRequest();
            foreach (var name in _headers.Where(header => header.Value.DeferredValue is not null)
                         .Select(header => header.Key).ToArray())
            {
                _headers.Remove(name);
            }
        }
    }

    private void FinalizeDeferredHeadersCore()
    {
        foreach (var name in _headers.Keys.ToArray())
        {
            var output = _headers[name];
            if (output.DeferredValue is not { } resolve)
            {
                continue;
            }

            ResponseHeaderUtilities.RemoveCaseVariants(_context.Response.Headers, name);
            if (output.IsSuppressed)
            {
                continue;
            }

            var values = resolve(_context.Response.Headers);
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length > 0)
            {
                _context.Response.Headers[name] = values.ToArray();
            }

            _headers[name] = output with { Values = values.ToArray(), IsPresent = values.Length > 0 };
        }
    }

    private void Reconcile()
    {
        foreach (var name in _headers.Keys.ToArray())
        {
            var previous = _headers[name];
            if (previous.IsSuppressed)
            {
                continue;
            }

            var matches = _context.Response.Headers
                .Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
            {
                if (previous.DeferredValue is not null)
                {
                    if (previous.IsPresent)
                    {
                        _headers[name] = previous with { Values = [], IsPresent = false, IsSuppressed = true };
                    }
                }
                else
                {
                    _headers.Remove(name);
                }
                continue;
            }

            var values = matches.SelectMany(header =>
            {
                ArgumentNullException.ThrowIfNull(header.Value);
                return header.Value;
            }).ToArray();
            _headers[name] = previous.IsPresent && previous.Values.SequenceEqual(values, StringComparer.Ordinal)
                ? previous with { Values = values }
                : new HeaderOutput(values, Source: null);
        }
    }

    private void EnsureRequest()
    {
        if (_requestId != _context.RequestId || !ReferenceEquals(_response, _context.Response))
        {
            _requestId = _context.RequestId;
            _response = _context.Response;
            _headers.Clear();
        }
    }

    internal sealed record HeaderOutput(
        string[] Values,
        Type? Source,
        Func<IReadOnlyDictionary<string, string[]>, string[]>? DeferredValue = null,
        bool IsPresent = true,
        bool IsSuppressed = false);

    internal sealed record Snapshot(Guid RequestId, MockResponse Response, KeyValuePair<string, HeaderOutput>[] Headers);
}
