using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class CacheInfo
{
    internal bool AllowPrivateResponseCaching;
    internal bool CacheSetup = false;
    internal string CachingType = "prefer-external";
    internal string DownstreamCachingType = "none";
    internal bool MustRevalidate = true;
    internal bool ShouldBeCached = false;

    internal bool VaryByDeveloper = false;
    internal bool VaryByDeveloperGroups = false;
    internal string[]? VaryByHeaders;
    internal string[]? VaryByQueryParameters;

    public CacheInfo WithExecutedCacheLookup(bool isSetup = true)
    {
        CacheSetup = isSetup;
        return this;
    }

    public CacheInfo WithExecutedCacheLookup(CacheLookupConfig config)
    {
        CacheSetup = true;
        VaryByDeveloper = config.VaryByDeveloper;
        VaryByDeveloperGroups = config.VaryByDeveloperGroups;
        CachingType = config.CachingType ?? CachingType;
        DownstreamCachingType = config.DownstreamCachingType ?? DownstreamCachingType;
        MustRevalidate = config.MustRevalidate ?? MustRevalidate;
        AllowPrivateResponseCaching = config.AllowPrivateResponseCaching ?? AllowPrivateResponseCaching;
        VaryByHeaders = config.VaryByHeaders;
        VaryByQueryParameters = config.VaryByQueryParameters;
        return this;
    }

    internal static string CacheKey(GatewayContext context)
    {
        var keyBuilder = new StringBuilder("key:");

        if (context.Product is not null)
        {
            keyBuilder.Append("&product:").Append(context.Product.Id).Append(':');
        }

        keyBuilder.Append("&api:").Append(context.Api.Id).Append(':');
        keyBuilder.Append("&operation:").Append(context.Operation.Id).Append(':');

        ProcessVaryBy(keyBuilder, "&params:", context.CacheInfo.VaryByQueryParameters, context.Request.Url.Query);
        ProcessVaryBy(keyBuilder, "&headers:", context.CacheInfo.VaryByHeaders, context.Request.Headers);

        if (context.CacheInfo.VaryByDeveloper)
        {
            keyBuilder.Append("&bydeveloper:").Append(context.User?.Id);
        }

        if (context.CacheInfo.VaryByDeveloperGroups)
        {
            keyBuilder.Append("&bygroups:");
            if (context.User is not null)
            {
                keyBuilder.AppendJoin(",", context.User.Groups.Select(g => g.Id));
            }
        }

        return keyBuilder.ToString();
    }

    private static void ProcessVaryBy(StringBuilder builder, string prefix, string[]? keys,
        Dictionary<string, string[]> map)
    {
        if (keys is null || keys.Length == 0)
        {
            return;
        }

        builder.Append(prefix);
        var keyList = keys.ToList();
        keyList.Sort(StringComparer.InvariantCultureIgnoreCase);
        foreach (var key in keyList)
        {
            if (!map.TryGetValue(key, out var v))
            {
                continue;
            }

            builder.Append(key).Append('=').AppendJoin(",", v);
        }
    }
}