// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CacheLookupHandler : PolicyHandler<CacheLookupConfig>
{
    internal const string LookupStateVariable = "__cache_lookup_state";

    internal sealed record LookupState(
        string Key, string CachingType, bool Cacheable, string DownstreamCachingType, bool MustRevalidate);

    public override string PolicyName => nameof(IInboundContext.CacheLookup);

    protected override void Handle(GatewayContext context, CacheLookupConfig config)
    {
        var cachingType = CachePolicyServices.GetCachingType(config.CachingType);
        var downstream = config.DownstreamCachingType ?? "none";
        if (downstream is not ("none" or "private" or "public" or "internal"))
        {
            throw new ArgumentException($"Unrecognized downstream caching type '{downstream}'.",
                nameof(config.DownstreamCachingType));
        }

        var key = BuildCacheKey(context, config);
        var cacheable = string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase)
            && (config.AllowPrivateResponseCaching == true || GetHeader(context, "Authorization") is null);
        context.Variables["__cache_lookup_key"] = key;
        context.Variables["__cache_hit"] = false;
        context.Variables[LookupStateVariable] = new LookupState(
            key, cachingType, cacheable, downstream, config.MustRevalidate ?? true);
        if (!cacheable)
        {
            return;
        }

        var cache = CachePolicyServices.Resolve(context, cachingType);
        if (cache is null)
        {
            return;
        }

        var cachedValue = cache.GetAsync(key).GetAwaiter().GetResult();
        var now = CachePolicyServices.GetTimeProvider(context, cache).GetUtcNow();
        var expiresAt = (cachedValue as CachedResponse)?.ExpiresAt;
        if (expiresAt is { } expiration && now >= expiration)
        {
            return;
        }

        if (!ResponseUtilities.TryCopyCachedResponse(cachedValue, context.Response))
        {
            return;
        }

        CachePolicyServices.SetCacheControl(context.Response.Headers,
            GetCacheControl(downstream, config.MustRevalidate ?? true,
                expiresAt is { } expiry ? expiry - now : TimeSpan.Zero));
        context.Variables["__cache_hit"] = true;
        context.ResponseTerminated = true;
        throw new FinishSectionProcessingException();
    }

    internal static string BuildCacheKey(GatewayContext context, CacheLookupConfig config)
    {
        var sb = new StringBuilder();
        sb.Append(context.Request.Url.ToUri().GetLeftPart(UriPartial.Path));

        foreach (var header in (config.VaryByHeaders ?? [])
            .Select(header =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(header);
                return header.ToLowerInvariant();
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            AppendValues(sb, "h", header, GetHeader(context, header));
        }

        var queryParameters = config.VaryByQueryParameters ?? context.Request.Url.Query.Keys.ToArray();
        foreach (var parameter in queryParameters.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
            context.Request.Url.Query.TryGetValue(parameter, out var values);
            AppendValues(sb, "q", parameter, values);
        }

        if (config.VaryByDeveloper)
        {
            ArgumentNullException.ThrowIfNull(context.User);
            ArgumentNullException.ThrowIfNull(context.Subscription);
            AppendValues(sb, "d", "developer", [context.User.Id, context.Subscription.Key]);
        }
        if (config.VaryByDeveloperGroups)
        {
            ArgumentNullException.ThrowIfNull(context.User);
            AppendValues(sb, "g", "groups",
                context.User.Groups.Select(group => group.Id).Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal).ToArray());
        }

        return sb.ToString();
    }

    internal static string GetCacheControl(string downstream, bool mustRevalidate, TimeSpan remaining)
    {
        if (downstream is "none" or "internal")
        {
            return "no-store";
        }

        var seconds = ((long)Math.Max(0, Math.Floor(remaining.TotalSeconds))).ToString(CultureInfo.InvariantCulture);
        return $"{downstream}, max-age={seconds}" + (mustRevalidate ? ", must-revalidate" : string.Empty);
    }

    private static string[]? GetHeader(GatewayContext context, string name)
    {
        var headers = context.Request.Headers;
        return headers.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            ? SchemaValidationSession.HeaderValues(headers, name)
            : null;
    }

    private static void AppendValues(StringBuilder key, string kind, string name, string[]? values)
    {
        key.Append('|').Append(kind).Append(':').Append(name.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(name).Append(':').Append((values?.Length ?? -1).ToString(CultureInfo.InvariantCulture));
        foreach (var value in values ?? [])
        {
            ArgumentNullException.ThrowIfNull(value);
            key.Append(':').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
    }
}