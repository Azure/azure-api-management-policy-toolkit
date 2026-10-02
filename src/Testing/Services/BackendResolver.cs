// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Resolves named backends and Service Fabric/Dapr routing in tests without contacting external services.
/// Register an implementation in <see cref="GatewayContext.Services"/>. The complete evaluated configuration
/// is supplied so that none of its routing options are silently discarded.
/// </summary>
public interface IBackendResolver
{
    /// <summary>Returns the absolute HTTP(S) base URI for the configured backend.</summary>
    Uri Resolve(GatewayContext context, SetBackendServiceConfig config);
}

internal static class BackendResolver
{
    public static Uri Resolve(GatewayContext context, SetBackendServiceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.BaseUrl is not null && config.BackendId is not null)
        {
            throw new ArgumentException("Specify either BaseUrl or BackendId, not both.", nameof(config));
        }

        ValidateOptionalValue(config.BackendId, nameof(config.BackendId));
        ValidateOptionalValue(config.SfServiceInstanceName, nameof(config.SfServiceInstanceName));
        ValidateOptionalValue(config.SfPartitionKey, nameof(config.SfPartitionKey));
        ValidateOptionalValue(config.SfListenerName, nameof(config.SfListenerName));
        ValidateOptionalValue(config.SfReplicaType, nameof(config.SfReplicaType));
        ValidateOptionalValue(config.DaprAppId, nameof(config.DaprAppId));
        ValidateOptionalValue(config.DaprMethod, nameof(config.DaprMethod));
        ValidateOptionalValue(config.DaprNamespace, nameof(config.DaprNamespace));

        if (config.DaprAppId is null && (config.DaprMethod is not null || config.DaprNamespace is not null))
        {
            throw new ArgumentException("DaprMethod and DaprNamespace require DaprAppId.", nameof(config));
        }

        var baseUri = config.BaseUrl is not null
            ? HttpTransportRequestBuilder.ParseHttpUri(config.BaseUrl, nameof(config.BaseUrl))
            : null;
        if (baseUri is null && config.BackendId is null && config.DaprAppId is null)
        {
            throw new ArgumentException("A BaseUrl, BackendId, or resolved DaprAppId is required.", nameof(config));
        }

        var requiresResolution = config.BackendId is not null
            || config.SfResolveCondition.HasValue
            || config.SfServiceInstanceName is not null
            || config.SfPartitionKey is not null
            || config.SfListenerName is not null
            || config.SfReplicaType is not null
            || config.DaprAppId is not null
            || config.DaprMethod is not null
            || config.DaprNamespace is not null;
        if (!requiresResolution && baseUri is not null)
        {
            return baseUri;
        }

        var resolver = context.Services.Resolve<IBackendResolver>()
            ?? throw new InvalidOperationException(
                "No IBackendResolver registered. Register one via Context.Services.Register<IBackendResolver>() " +
                "to resolve backend IDs and Service Fabric/Dapr routing options.");
        var resolved = resolver.Resolve(context, config);
        if (resolved is null || !HttpTransportRequestBuilder.IsHttpUri(resolved))
        {
            throw new InvalidOperationException("IBackendResolver must return an absolute HTTP(S) URI without credentials or a fragment.");
        }

        return resolved;
    }

    public static Uri ForwardUri(GatewayContext context)
    {
        var original = HttpTransportRequestBuilder.ParseHttpUri(context.Request.Url.ToString(), "Request.Url");
        return context.BackendUrl is null
            ? original
            : Combine(HttpTransportRequestBuilder.ParseHttpUri(context.BackendUrl, nameof(context.BackendUrl)),
                original.AbsolutePath, original.Query);
    }

    public static Uri InvokeUri(GatewayContext context, InvokeRequestConfig config)
    {
        if (config.BackendId is null)
        {
            return config.Url is null
                ? ForwardUri(context)
                : HttpTransportRequestBuilder.ParseHttpUri(config.Url, nameof(config.Url));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(config.BackendId);
        if (config.Url is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.Url);
            if (!Uri.TryCreate(config.Url, UriKind.Relative, out _)
                || config.Url.StartsWith("//", StringComparison.Ordinal)
                || config.Url.Contains('\\')
                || config.Url.Contains('#')
                || config.Url.Any(char.IsControl)
                || config.Url != config.Url.Trim())
            {
                throw new ArgumentException("Url must be a relative path when BackendId is specified.", nameof(config.Url));
            }
        }

        var backend = Resolve(context, new SetBackendServiceConfig { BackendId = config.BackendId });
        if (config.Url is null)
        {
            var original = HttpTransportRequestBuilder.ParseHttpUri(context.Request.Url.ToString(), "Request.Url");
            return Combine(backend, original.AbsolutePath, original.Query);
        }

        var separator = config.Url.IndexOf('?');
        return separator < 0
            ? Combine(backend, config.Url, "")
            : Combine(backend, config.Url[..separator], config.Url[(separator + 1)..]);
    }

    private static Uri Combine(Uri backend, string path, string query)
    {
        var backendQuery = backend.Query.TrimStart('?');
        var requestQuery = query.TrimStart('?');
        var combinedQuery = backendQuery.Length == 0 ? requestQuery
            : requestQuery.Length == 0 ? backendQuery
            : $"{backendQuery}&{requestQuery}";
        return new UriBuilder(backend)
        {
            Path = $"{backend.AbsolutePath.TrimEnd('/')}/{path.TrimStart('/')}",
            Query = combinedQuery
        }.Uri;
    }

    private static void ValidateOptionalValue(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}