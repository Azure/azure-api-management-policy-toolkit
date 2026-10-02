// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IOutboundContext))]
internal partial class RedirectContentUrlsHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, bool>,
        Action<GatewayContext>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IOutboundContext.RedirectContentUrls);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        if (args is { Length: > 0 })
        {
            throw new ArgumentException("RedirectContentUrls does not accept arguments.", nameof(args));
        }

        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context);
            return null;
        }

        MockMessage message = context.CurrentSectionName switch
        {
            nameof(IInboundContext) => context.Request,
            nameof(IOutboundContext) => context.Response,
            _ => throw new InvalidOperationException("RedirectContentUrls requires an inbound or outbound section."),
        };
        var body = message.Body.Content;
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var backend = HttpUrl(
            context.BackendUrl ?? context.Api.ServiceUrl.ToString(), nameof(context.BackendUrl));
        if (backend.Query.Length != 0 || backend.Fragment.Length != 0)
        {
            throw new ArgumentException("The backend base URL must not contain a query or fragment.", nameof(context.BackendUrl));
        }

        var original = HttpUrl(context.Request.OriginalUrl.ToString(), nameof(context.Request.OriginalUrl));
        var apiPath = context.Api.Path;
        ArgumentNullException.ThrowIfNull(apiPath, nameof(context.Api.Path));
        if (apiPath.IndexOfAny(['?', '#', '\\']) >= 0
            || !Uri.IsWellFormedUriString("/" + apiPath.Trim('/'), UriKind.Relative))
        {
            throw new ArgumentException("The public API path must be a well-formed URL path.", nameof(context.Api.Path));
        }

        var publicUrl = new UriBuilder(original)
        {
            Path = "/" + apiPath.Trim('/'),
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
        var inbound = context.CurrentSectionName == nameof(IInboundContext);
        var source = inbound ? publicUrl : backend;
        var destination = inbound ? backend : publicUrl;
        var rewritten = ContentUrlRegex().Replace(body, match => RewriteLink(match, source, destination));
        if (rewritten == body)
        {
            return null;
        }

        var contentLengthHeaders = message.Headers.Keys
            .Where(name => name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contentLength = Encoding.UTF8.GetByteCount(rewritten).ToString(CultureInfo.InvariantCulture);

        message.Body.Content = rewritten;
        foreach (var header in contentLengthHeaders)
        {
            message.Headers[header] = [contentLength];
        }

        return null;
    }

    private static Uri HttpUrl(string value, string parameter)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            || !url.IsWellFormedOriginalString()
            || url.UserInfo.Length != 0)
        {
            throw new ArgumentException("URL mapping requires an absolute HTTP(S) URL without user information.", parameter);
        }

        return url;
    }

    private static string RewriteLink(Match match, Uri source, Uri destination)
    {
        var group = match.Groups["url"];
        var value = group.Value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url)
            || !url.IsWellFormedOriginalString()
            || url.UserInfo.Length != 0
            || url.Scheme != source.Scheme
            || !url.Host.Equals(source.Host, StringComparison.OrdinalIgnoreCase)
            || url.Port != source.Port)
        {
            return match.Value;
        }

        var authorityStart = value.IndexOf("://", StringComparison.Ordinal) + 3;
        var suffixIndex = value.IndexOfAny(['/', '?', '#'], authorityStart);
        if (suffixIndex < 0)
        {
            suffixIndex = value.Length;
        }

        var pathEnd = value.IndexOfAny(['?', '#'], suffixIndex);
        if (pathEnd < 0)
        {
            pathEnd = value.Length;
        }

        var sourcePath = source.AbsolutePath.TrimEnd('/');
        var canonicalPath = url.AbsolutePath;
        var path = value[suffixIndex..pathEnd];
        if (sourcePath.Length != 0)
        {
            if (!HasBasePath(canonicalPath, sourcePath) || !HasBasePath(path, sourcePath))
            {
                return match.Value;
            }
        }

        var destinationPath = destination.AbsolutePath.TrimEnd('/');
        var pathSuffix = path.Length == 0 ? string.Empty : canonicalPath[sourcePath.Length..];
        var rewrittenPath = destinationPath + pathSuffix;
        // Do not emit residual encoded dot segments if the runtime left them unnormalized.
        if (HasDotSegments(rewrittenPath))
        {
            return match.Value;
        }

        var replacement = destination.GetLeftPart(UriPartial.Authority)
            + rewrittenPath
            + value[pathEnd..];
        if (!Uri.TryCreate(replacement, UriKind.Absolute, out var rewritten)
            || !HasBasePath(rewritten.AbsolutePath, destinationPath))
        {
            return match.Value;
        }

        var offset = group.Index - match.Index;
        return match.Value[..offset] + replacement + match.Value[(offset + group.Length)..];
    }

    private static bool HasDotSegments(string path) =>
        path.Split('/').Any(segment => Uri.UnescapeDataString(segment) is "." or "..");

    private static bool HasBasePath(string path, string basePath) =>
        path.Equals(basePath, StringComparison.Ordinal) || path.StartsWith(basePath + '/', StringComparison.Ordinal);

    [GeneratedRegex(
        """(?<quote>["'])(?<url>https?://[^<>]*?)\k<quote>|(?<![\w:/@.\-])(?<url>https?://[^\s<>"']+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ContentUrlRegex();
}
