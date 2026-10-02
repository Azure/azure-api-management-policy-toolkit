// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CorsHandler : PolicyHandler<CorsConfig>
{
    private static readonly HashSet<string> s_responseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Access-Control-Allow-Origin",
        "Access-Control-Allow-Credentials",
        "Access-Control-Allow-Methods",
        "Access-Control-Allow-Headers",
        "Access-Control-Expose-Headers",
        "Access-Control-Max-Age"
    };

    public override string PolicyName => nameof(IInboundContext.Cors);

    protected override void Handle(GatewayContext context, CorsConfig config)
    {
        var terminateUnmatched = ValidateConfig(config);
        PolicyResponseHeaderOverlay.RemoveGeneratedHeaders(context, typeof(CorsHandler));
        if (!HasHeader(context.Request.Headers, "Origin"))
        {
            return;
        }

        var allowedOrigin = MatchOrigin(config, HeaderValues(context.Request.Headers, "Origin"));
        if (allowedOrigin is null)
        {
            HandleUnmatched(context, terminateUnmatched);
            return;
        }

        var preflight = context.Request.Method == "OPTIONS"
                        && HasHeader(context.Request.Headers, "Access-Control-Request-Method");
        if (!preflight)
        {
            var exposedHeaders = ExposedHeaders(context, config);
            foreach (var name in s_responseHeaders)
            {
                ResponseHeaderUtilities.RemoveCaseVariants(context.Response.Headers, name);
            }

            PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Allow-Origin", [allowedOrigin], typeof(CorsHandler));
            AddCredentials(context, config);
            if (config.AllowCredentials == true
                && config.ExposeHeaders is { } configured
                && configured.Contains("*", StringComparer.Ordinal))
            {
                var declared = configured.ToArray();
                PolicyResponseHeaderOverlay.SetDeferredHeader(context, "Access-Control-Expose-Headers",
                    exposedHeaders.Length == 0 ? [] : [string.Join(",", exposedHeaders)], typeof(CorsHandler),
                    headers =>
                    {
                        var actual = ExpandExposedHeaders(headers.Keys, declared);
                        return actual.Length == 0 ? [] : [string.Join(",", actual)];
                    });
            }
            else if (exposedHeaders.Length > 0)
            {
                PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Expose-Headers",
                    [string.Join(",", exposedHeaders)], typeof(CorsHandler));
            }
            return;
        }

        var methods = config.AllowedMethods ?? ["GET", "POST"];
        var requestedMethods = HeaderValues(context.Request.Headers, "Access-Control-Request-Method");
        if (requestedMethods.Length != 1 || !IsToken(requestedMethods[0])
            || (!methods.Contains("*", StringComparer.Ordinal)
                && !methods.Contains(requestedMethods[0], StringComparer.Ordinal))
            || !TryRequestedHeaders(context.Request.Headers, out var requestedHeaders)
            || (!config.AllowedHeaders.Contains("*", StringComparer.Ordinal)
                && requestedHeaders.Any(header =>
                    !config.AllowedHeaders.Contains(header, StringComparer.OrdinalIgnoreCase))))
        {
            HandleUnmatched(context, terminateUnmatched);
            return;
        }

        ResponseUtilities.Overwrite(context.Response, 200, "OK");
        PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Allow-Origin", [allowedOrigin], typeof(CorsHandler));
        AddCredentials(context, config);
        PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Allow-Methods",
            [methods.Contains("*", StringComparer.Ordinal) ? requestedMethods[0] : string.Join(",", methods)], typeof(CorsHandler));

        var allowedHeaders = config.AllowedHeaders.Contains("*", StringComparer.Ordinal)
            ? requestedHeaders
            : config.AllowedHeaders;
        if (allowedHeaders.Length > 0)
        {
            PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Allow-Headers",
                [string.Join(",", allowedHeaders)], typeof(CorsHandler));
        }

        if (config.PreflightResultMaxAge is not null)
        {
            PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Max-Age",
                [config.PreflightResultMaxAge.Value.ToString(CultureInfo.InvariantCulture)], typeof(CorsHandler));
        }

        throw new FinishSectionProcessingException();
    }

    private static bool ValidateConfig(CorsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config.AllowedOrigins);
        ArgumentNullException.ThrowIfNull(config.AllowedHeaders);
        if (config.AllowedOrigins.Length == 0)
        {
            throw new ArgumentException("At least one allowed origin is required.", nameof(config.AllowedOrigins));
        }
        if (config.AllowedOrigins.Contains("*", StringComparer.Ordinal) && config.AllowedOrigins.Length != 1)
        {
            throw new ArgumentException("The origin wildcard must be the only allowed origin.",
                nameof(config.AllowedOrigins));
        }

        foreach (var origin in config.AllowedOrigins)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(origin, nameof(config.AllowedOrigins));
            if (origin != "*" && !TryOrigin(origin, out _))
            {
                throw new ArgumentException($"'{origin}' is not a valid HTTP origin.", nameof(config.AllowedOrigins));
            }
        }

        if (config.AllowedMethods is not null)
        {
            if (config.AllowedMethods.Length == 0)
            {
                throw new ArgumentException("At least one allowed method is required.", nameof(config.AllowedMethods));
            }
            ValidateTokens(config.AllowedMethods, nameof(config.AllowedMethods));
        }
        ValidateTokens(config.AllowedHeaders, nameof(config.AllowedHeaders));
        if (config.ExposeHeaders is not null)
        {
            ValidateTokens(config.ExposeHeaders, nameof(config.ExposeHeaders));
        }

        if (config.TerminateUnmatchedRequest is null)
        {
            return true;
        }
        if (!bool.TryParse(config.TerminateUnmatchedRequest, out var terminateUnmatched))
        {
            throw new ArgumentException("TerminateUnmatchedRequest must be 'true' or 'false'.",
                nameof(config.TerminateUnmatchedRequest));
        }
        return terminateUnmatched;
    }

    private static void ValidateTokens(string[] tokens, string parameter)
    {
        foreach (var token in tokens)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token, parameter);
            if (!IsToken(token))
            {
                throw new ArgumentException($"'{token}' is not a valid HTTP token.", parameter);
            }
        }
    }

    private static bool IsToken(string value) => !string.IsNullOrEmpty(value)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));

    private static bool TryOrigin(string value, out Uri? origin)
    {
        origin = null;
        if (value == "null")
        {
            return true;
        }

        return value == value.Trim() && !value.Any(char.IsControl)
            && Uri.TryCreate(value, UriKind.Absolute, out origin)
            && origin.Scheme is "http" or "https"
            && origin.Host.Length > 0 && origin.UserInfo.Length == 0
            && origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0;
    }

    private static string? MatchOrigin(CorsConfig config, string[] values)
    {
        if (values.Length != 1 || values[0] is null || !TryOrigin(values[0], out var requested))
        {
            return null;
        }

        var origin = values[0];
        if (config.AllowedOrigins.Contains("*", StringComparer.Ordinal))
        {
            return config.AllowCredentials == true ? origin : "*";
        }

        foreach (var allowed in config.AllowedOrigins)
        {
            if (allowed == "null")
            {
                if (requested is null)
                {
                    return origin;
                }
                continue;
            }
            if (requested is null)
            {
                continue;
            }

            var parsed = new Uri(allowed, UriKind.Absolute);
            if (string.Equals(parsed.Scheme, requested.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(parsed.IdnHost, requested.IdnHost, StringComparison.OrdinalIgnoreCase)
                && parsed.Port == requested.Port)
            {
                return origin;
            }
        }
        return null;
    }

    private static bool HasHeader(Dictionary<string, string[]> headers, string name) =>
        headers.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static string[] HeaderValues(Dictionary<string, string[]> headers, string name) =>
        headers.Where(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(header =>
            {
                ArgumentNullException.ThrowIfNull(header.Value);
                return header.Value;
            }).ToArray();

    private static bool TryRequestedHeaders(Dictionary<string, string[]> headers, out string[] requested)
    {
        var names = new List<string>();
        foreach (var value in HeaderValues(headers, "Access-Control-Request-Headers"))
        {
            if (value is null)
            {
                requested = [];
                return false;
            }
            foreach (var token in value.Split(','))
            {
                var name = token.Trim(' ', '\t');
                if (!IsToken(name))
                {
                    requested = [];
                    return false;
                }
                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(name);
                }
            }
        }
        requested = names.ToArray();
        return true;
    }

    private static string[] ExposedHeaders(GatewayContext context, CorsConfig config)
    {
        var exposed = config.ExposeHeaders ?? [];
        if (config.AllowCredentials != true || !exposed.Contains("*", StringComparer.Ordinal))
        {
            return exposed;
        }

        return ExpandExposedHeaders(context.Response.Headers.Keys, exposed);
    }

    private static string[] ExpandExposedHeaders(IEnumerable<string> names, string[] exposed) =>
        exposed.Where(name => name != "*")
            .Concat(names.Where(name => !s_responseHeaders.Contains(name)))
            .Where(name => !string.Equals(name, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(name, "Set-Cookie2", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void AddCredentials(GatewayContext context, CorsConfig config)
    {
        if (config.AllowCredentials == true)
        {
            PolicyResponseHeaderOverlay.SetHeader(context, "Access-Control-Allow-Credentials", ["true"], typeof(CorsHandler));
        }
    }

    private static void HandleUnmatched(GatewayContext context, bool terminate)
    {
        if (!terminate)
        {
            return;
        }

        ResponseUtilities.Overwrite(context.Response, 200, "OK");
        throw new FinishSectionProcessingException();
    }
}