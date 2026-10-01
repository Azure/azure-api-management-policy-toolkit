// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

// Public mock messages expose ordinary dictionaries. Publish deltas under one lock instead of
// sharing those dictionaries across blocked handlers, callbacks, and asynchronous transports.
internal sealed class WaitMessageSynchronization(GatewayContext parent)
{
    private readonly object _sync = new();
    private readonly object _traceSync = new();

    internal GatewayContext Owner => parent;
    internal MockRequest RequestIdentity => parent.Request;

    internal void Trace(string message)
    {
        lock (_traceSync)
        {
            parent.Trace(message);
        }
    }

    internal WaitMessageSnapshot Capture(GatewayContext context) => new(
        CopyRequest(context.Request), CopyResponse(context.Response), context.BackendUrl,
        context.Services.Resolve<HttpTransportState>()?.Proxy,
        PolicyResponseHeaderOverlay.Existing(context)?.Capture(),
        context.Request, context.Response, context.Request.Body, context.Response.Body,
        context.Request.Body.ContentWriteVersion, context.Response.Body.ContentWriteVersion,
        context.Response.StatusCodeWriteVersion, context.Response.StatusReasonWriteVersion,
        context.BackendResponseVersion, context.TerminalResponseVersion,
        CaptureHeaders(context.Request.Headers), CaptureHeaders(context.Response.Headers));

    private static WaitHeaderSnapshot CaptureHeaders(Dictionary<string, string[]> headers) =>
        new(headers, new Dictionary<string, string[]>(headers, headers.Comparer));

    internal void Refresh(GatewayContext branch)
    {
        lock (_sync)
        {
            CopyProperties(branch.Request, parent.Request);
            CopyProperties(branch.Response, parent.Response);
            branch.BackendUrl = parent.BackendUrl;
            var transport = branch.Services.Resolve<HttpTransportState>();
            if (transport is not null)
            {
                transport.Proxy = parent.Services.Resolve<HttpTransportState>()?.Proxy;
            }
            PolicyResponseHeaderOverlay.CopyForWait(parent, branch);
            branch.CopyBackendStateFrom(parent);
        }
    }

    internal WaitMessageSnapshot Synchronize(
        GatewayContext branch, WaitMessageSnapshot previous, CancellationToken cancellationToken,
        IReadOnlyList<WaitHeaderMutation> requestHeaders, IReadOnlyList<WaitHeaderMutation> responseHeaders)
    {
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PublishCore(branch, previous, requestHeaders, responseHeaders);
            Refresh(branch);
            return Capture(branch);
        }
    }

    internal WaitMessageSnapshot Publish(
        GatewayContext branch, WaitMessageSnapshot previous, CancellationToken cancellationToken,
        IReadOnlyList<WaitHeaderMutation> requestHeaders, IReadOnlyList<WaitHeaderMutation> responseHeaders)
    {
        lock (_sync)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                PublishCore(branch, previous, requestHeaders, responseHeaders);
            }
            return Capture(branch);
        }
    }

    private void PublishCore(
        GatewayContext branch, WaitMessageSnapshot previous,
        IReadOnlyList<WaitHeaderMutation> requestHeaders, IReadOnlyList<WaitHeaderMutation> responseHeaders)
    {
        if (!ReferenceEquals(previous.RequestReference, branch.Request))
        {
            CopyProperties(parent.Request, branch.Request);
        }
        else
        {
            ApplyDelta(parent.Request, previous.Request, branch.Request, previous.RequestHeaders, requestHeaders);
            if (!ReferenceEquals(previous.RequestBodyReference, branch.Request.Body)
                || previous.RequestContentVersion != branch.Request.Body.ContentWriteVersion)
            {
                parent.Request.Body.Content = branch.Request.Body.Content;
            }
        }
        if (!ReferenceEquals(previous.ResponseReference, branch.Response)
            || previous.BackendVersion != branch.BackendResponseVersion
            || previous.TerminalVersion != branch.TerminalResponseVersion)
        {
            CopyProperties(parent.Response, branch.Response);
        }
        else
        {
            ApplyDelta(parent.Response, previous.Response, branch.Response, previous.ResponseHeaders, responseHeaders);
            if (!ReferenceEquals(previous.ResponseBodyReference, branch.Response.Body)
                || previous.ResponseContentVersion != branch.Response.Body.ContentWriteVersion)
            {
                parent.Response.Body.Content = branch.Response.Body.Content;
            }
            if (previous.StatusCodeVersion != branch.Response.StatusCodeWriteVersion)
            {
                parent.Response.StatusCode = branch.Response.StatusCode;
            }
            if (previous.StatusReasonVersion != branch.Response.StatusReasonWriteVersion)
            {
                parent.Response.StatusReason = branch.Response.StatusReason;
            }
        }
        if (previous.BackendUrl != branch.BackendUrl)
        {
            parent.BackendUrl = branch.BackendUrl;
        }
        var proxy = branch.Services.Resolve<HttpTransportState>()?.Proxy;
        if (!Equals(previous.Proxy, proxy))
        {
            HttpPolicyTransport.GetState(parent).Proxy = proxy;
        }
        PolicyResponseHeaderOverlay.MergeFromWait(parent, branch, previous.Headers);
        parent.MergeBackendStateFrom(branch);
    }

    internal static MockRequest CopyRequest(MockRequest request)
    {
        var result = new MockRequest();
        CopyProperties(result, request);
        return result;
    }

    internal static MockResponse CopyResponse(MockResponse response)
    {
        var result = new MockResponse();
        CopyProperties(result, response);
        return result;
    }

    private static IEnumerable<PropertyInfo> Properties(Type type) => type.GetProperties()
        .Where(property => property.GetIndexParameters().Length == 0 && property.GetSetMethod(true) is not null
            && !(type == typeof(MockUrl) && property.Name == nameof(MockUrl.QueryString)));

    private static object? CopyValue(object? value) => value switch
    {
        null => null,
        string[] values => values.ToArray(),
        Dictionary<string, string[]> values => values.ToDictionary(
            entry => entry.Key, entry => entry.Value.ToArray(), values.Comparer),
        Dictionary<string, string> values => new Dictionary<string, string>(values, values.Comparer),
        MockBody body => body.CopyForWait(),
        MockUrl url => CopyModel(url),
        MockSubscriptionKeyParameterNames names => CopyModel(names),
        MockContextApi api => CopyModel(api),
        MockApi api => CopyModel(api),
        MockGroup group => CopyModel(group),
        MockUserIdentity identity => CopyModel(identity),
        List<MockGroup> groups => groups.Select(CopyModel).ToList(),
        List<MockUserIdentity> identities => identities.Select(CopyModel).ToList(),
        Dictionary<string, X509Certificate2> certificates =>
            new Dictionary<string, X509Certificate2>(certificates, certificates.Comparer),
        MockPrivateEndpointConnection endpoint => CopyModel(endpoint),
        MockAzureVnetInfo vnet => CopyModel(vnet),
        _ => value
    };

    internal static T CopyModel<T>(T source) where T : class, new()
    {
        var target = new T();
        CopyProperties(target, source);
        return target;
    }

    private static void CopyProperties(object target, object source)
    {
        foreach (var property in Properties(target.GetType()))
        {
            var value = property.GetValue(source);
            var existing = property.GetValue(target);
            if (existing is MockBody or MockUrl && value is not null)
            {
                CopyProperties(existing, value);
            }
            else
            {
                property.SetValue(target, CopyValue(value));
            }
        }
    }

    private static void ApplyDelta(
        object target, object previous, object current,
        WaitHeaderSnapshot? headerSnapshot = null, IReadOnlyList<WaitHeaderMutation>? headerMutations = null)
    {
        foreach (var property in Properties(target.GetType()))
        {
            var before = property.GetValue(previous);
            var after = property.GetValue(current);
            if (before is Dictionary<string, string[]> oldHeaders && after is Dictionary<string, string[]> newHeaders)
            {
                if (headerSnapshot is not null && !ReferenceEquals(headerSnapshot.Reference, newHeaders))
                {
                    property.SetValue(target, CopyValue(newHeaders));
                    continue;
                }
                var headers = (Dictionary<string, string[]>)property.GetValue(target)!;
                MergeDictionary(headers, oldHeaders, newHeaders,
                    (first, second) => first.SequenceEqual(second), values => values.ToArray(),
                    (name, values) => headerSnapshot is not null
                        && (!headerSnapshot.Values.TryGetValue(name, out var original) || !ReferenceEquals(original, values)));
                foreach (var mutation in headerMutations ?? [])
                {
                    if (mutation.RemoveCaseVariants)
                    {
                        ResponseHeaderUtilities.RemoveCaseVariants(headers, mutation.Name);
                    }
                    else
                    {
                        headers.Remove(mutation.Name);
                    }
                    foreach (var entry in newHeaders.Where(entry => mutation.RemoveCaseVariants
                                 ? entry.Key.Equals(mutation.Name, StringComparison.OrdinalIgnoreCase)
                                 : newHeaders.Comparer.Equals(entry.Key, mutation.Name)))
                    {
                        headers[entry.Key] = entry.Value.ToArray();
                    }
                }
            }
            else if (before is Dictionary<string, string> oldValues && after is Dictionary<string, string> newValues)
            {
                MergeDictionary((Dictionary<string, string>)property.GetValue(target)!, oldValues, newValues,
                    (first, second) => first == second, value => value);
            }
            else if (before is MockBody or MockUrl or MockPrivateEndpointConnection or MockAzureVnetInfo
                && after is not null && property.GetValue(target) is { } nested)
            {
                ApplyDelta(nested, before, after);
            }
            else if (!Equals(before, after))
            {
                property.SetValue(target, CopyValue(after));
            }
        }
    }

    private static void MergeDictionary<T>(
        Dictionary<string, T> target, Dictionary<string, T> before, Dictionary<string, T> after,
        Func<T, T, bool> equal, Func<T, T> copy, Func<string, T, bool>? explicitChange = null)
    {
        foreach (var entry in after)
        {
            if (!before.TryGetValue(entry.Key, out var original) || !equal(original, entry.Value)
                || explicitChange?.Invoke(entry.Key, entry.Value) == true)
            {
                target[entry.Key] = copy(entry.Value);
            }
        }
        foreach (var name in before.Keys.Where(name => !after.ContainsKey(name)))
        {
            target.Remove(name);
        }
    }
}

internal sealed record WaitMessageSnapshot(
    MockRequest Request, MockResponse Response, string? BackendUrl,
    Authoring.ProxyConfig? Proxy, PolicyResponseHeaderOverlay.Snapshot? Headers,
    MockRequest RequestReference, MockResponse ResponseReference,
    MockBody RequestBodyReference, MockBody ResponseBodyReference,
    long RequestContentVersion, long ResponseContentVersion,
    long StatusCodeVersion, long StatusReasonVersion, long BackendVersion, long TerminalVersion,
    WaitHeaderSnapshot RequestHeaders, WaitHeaderSnapshot ResponseHeaders);

internal sealed record WaitHeaderSnapshot(
    Dictionary<string, string[]> Reference, Dictionary<string, string[]> Values);