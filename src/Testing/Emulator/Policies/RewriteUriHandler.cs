// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Web;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal partial class RewriteUriHandler : PolicyHandler<string, bool>
{
    public override string PolicyName => nameof(IInboundContext.RewriteUri);

    protected override void Handle(GatewayContext context, string template, bool copyUnmatchedParams)
    {
        var resolvedTemplate = ResolvePlaceholders(template, context.Request.MatchedParameters);

        var queryIndex = resolvedTemplate.IndexOf('?');
        string newPath;
        Dictionary<string, string[]> templateQueryParams;

        if (queryIndex >= 0)
        {
            newPath = resolvedTemplate[..queryIndex];
            templateQueryParams = ParseQueryString(resolvedTemplate[(queryIndex + 1)..]);
        }
        else
        {
            newPath = resolvedTemplate;
            templateQueryParams = new Dictionary<string, string[]>();
        }

        context.Request.Url.Path = newPath;

        if (copyUnmatchedParams)
        {
            foreach (var kvp in context.Request.Url.Query)
            {
                if (!templateQueryParams.ContainsKey(kvp.Key))
                {
                    templateQueryParams[kvp.Key] = kvp.Value;
                }
            }
        }

        context.Request.Url.Query = templateQueryParams;
    }

    private static string ResolvePlaceholders(string template, IReadOnlyDictionary<string, string> matchedParameters)
    {
        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!matchedParameters.TryGetValue(key, out var value))
            {
                throw new ArgumentException($"Template placeholder '{key}' not found in MatchedParameters");
            }

            return value;
        });
    }

    private static Dictionary<string, string[]> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string[]>();
        var nameValueCollection = HttpUtility.ParseQueryString(queryString);

        foreach (var key in nameValueCollection.AllKeys)
        {
            if (key is not null)
            {
                result[key] = nameValueCollection.GetValues(key) ?? [];
            }
        }

        return result;
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex PlaceholderRegex();
}