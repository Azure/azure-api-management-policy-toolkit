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
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        var resolvedTemplate = ResolvePlaceholders(template, context.Request.MatchedParameters);
        ValidateTemplate(resolvedTemplate);

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

        if (copyUnmatchedParams)
        {
            var operationTemplate = context.Operation.UrlTemplate;
            var operationQueryIndex = operationTemplate.IndexOf('?');
            var matchedQueryParams = operationQueryIndex < 0
                ? new Dictionary<string, string[]>()
                : ParseQueryString(operationTemplate[(operationQueryIndex + 1)..]);
            foreach (var kvp in context.Request.Url.Query)
            {
                if (!templateQueryParams.ContainsKey(kvp.Key) && !matchedQueryParams.ContainsKey(kvp.Key))
                {
                    templateQueryParams[kvp.Key] = kvp.Value.ToArray();
                }
            }
        }

        context.Request.Url.Path = newPath;
        context.Request.Url.Query = templateQueryParams;
    }

    private static void ValidateTemplate(string template)
    {
        if (!template.StartsWith('/') || template.StartsWith("//", StringComparison.Ordinal)
            || template.IndexOfAny(['#', '\\', '{', '}']) >= 0
            || template.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("RewriteUri requires an escaped root-relative path without fragments or unresolved placeholders.", nameof(template));
        }

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '%')
            {
                continue;
            }

            if (index + 2 >= template.Length || !Uri.IsHexDigit(template[index + 1]) || !Uri.IsHexDigit(template[index + 2]))
            {
                throw new ArgumentException("RewriteUri contains an invalid percent escape.", nameof(template));
            }

            index += 2;
        }
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

            ArgumentNullException.ThrowIfNull(value);
            return Uri.EscapeDataString(value);
        });
    }

    private static Dictionary<string, string[]> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var parameter in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = parameter.IndexOf('=');
            var key = HttpUtility.UrlDecode(separator < 0 ? parameter : parameter[..separator]);
            ArgumentException.ThrowIfNullOrEmpty(key, nameof(queryString));
            var value = separator < 0 ? string.Empty : HttpUtility.UrlDecode(parameter[(separator + 1)..]);
            ArgumentNullException.ThrowIfNull(value);
            result[key] = result.TryGetValue(key, out var values) ? [.. values, value] : [value];
        }

        return result;
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex PlaceholderRegex();
}