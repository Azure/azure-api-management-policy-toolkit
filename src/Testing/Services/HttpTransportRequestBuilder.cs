// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class HttpTransportRequestBuilder
{
    private static readonly HttpRequestOptionsKey<X509Certificate2> s_ownedCertificateKey = new("PolicyToolkit.OwnedClientCertificate");

    public static HttpRequestMessage Create(
        GatewayContext context,
        Uri target,
        string method,
        bool copyHeaders,
        bool copyBody,
        HeaderConfig[]? headers = null,
        BodyConfig? body = null,
        IAuthenticationConfig? authentication = null,
        ProxyConfig? proxy = null,
        HttpTransportOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(method) || !IsToken(method))
        {
            throw new ArgumentException("The HTTP method must be a nonempty HTTP token.", nameof(method));
        }

        if (body?.Template is not null || body?.XsiNil is not null || body?.ParseDate is not null)
        {
            throw new NotSupportedException("The HTTP policy emulator does not support body templates or template parsing options.");
        }

        var state = HttpPolicyTransport.GetState(context);
        var selectedProxy = proxy ?? state.Proxy;
        ValidateProxy(selectedProxy);
        var values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (copyHeaders)
        {
            foreach (var header in context.Request.Headers)
            {
                AppendHeader(values, header.Key, ValidateHeader(header.Key, header.Value));
            }
        }

        var deletedHeaders = ApplyHeaders(values, headers);
        var request = new HttpRequestMessage(new HttpMethod(method), target);
        var completed = false;
        try
        {
            if (body is not null)
            {
                request.Content = body.Content is byte[] bytes
                    ? new ByteArrayContent(bytes.ToArray())
                    : new StringContent(body.Content?.ToString() ?? "", GetStringBodyEncoding(values));
            }
            else if (copyBody && context.Request.Body.Content is not null)
            {
                request.Content = new StringContent(context.Request.Body.As<string>(preserveContent: true), GetStringBodyEncoding(values));
            }

            foreach (var header in values)
            {
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    continue;
                }

                request.Content ??= new ByteArrayContent([]);
                request.Content.Headers.Remove(header.Key);
                if (!request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    throw new ArgumentException($"Header '{header.Key}' cannot be added to an HTTP request.", nameof(headers));
                }
            }

            if (request.Content is not null)
            {
                foreach (var name in request.Content.Headers.Select(header => header.Key).Where(deletedHeaders.Contains).ToArray())
                {
                    request.Content.Headers.Remove(name);
                }

                request.Content.Headers.ContentLength = request.Headers.TransferEncodingChunked == true
                    ? null
                    : request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().LongLength;
            }

            var selectedOptions = (options ?? new HttpTransportOptions()) with
            {
                Proxy = selectedProxy,
                Certificate = copyHeaders ? context.Request.Certificate : null
            };
            selectedOptions = ApplyAuthentication(context, request, authentication, selectedOptions);
            request.Options.Set(HttpTransportOptions.Key, selectedOptions);
            completed = true;
            return request;
        }
        finally
        {
            if (!completed)
            {
                DisposeRequest(request);
            }
        }
    }

    public static bool CopyMode(string? mode)
    {
        if (mode is null || mode.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (mode.Equals("copy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new ArgumentException("Request mode must be 'new' or 'copy'.", nameof(mode));
    }

    public static Uri ParseHttpUri(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value != value.Trim() || value.Any(char.IsControl)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsHttpUri(uri))
        {
            throw new ArgumentException("An absolute HTTP(S) URL without credentials or a fragment is required.", parameterName);
        }

        return uri;
    }

    public static bool IsHttpUri(Uri uri) =>
        uri.IsAbsoluteUri
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);

    public static void ValidateProxy(ProxyConfig? config)
    {
        if (config is null)
        {
            return;
        }

        ParseHttpUri(config.Url, nameof(config.Url));
        if ((config.Username is null) != (config.Password is null))
        {
            throw new ArgumentException("Proxy username and password must be supplied together.", nameof(config));
        }
        if (config.Username is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(config.Username);
        }
    }

    public static void DisposeRequest(HttpRequestMessage request)
    {
        try
        {
            request.Dispose();
        }
        finally
        {
            if (request.Options.TryGetValue(s_ownedCertificateKey, out var certificate))
            {
                certificate.Dispose();
            }
        }
    }

    private static HashSet<string> ApplyHeaders(Dictionary<string, string[]> target, HeaderConfig[]? headers)
    {
        var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers ?? [])
        {
            ArgumentNullException.ThrowIfNull(header);
            var action = header.ExistsAction ?? "override";
            if (action is not ("override" or "append" or "skip" or "delete"))
            {
                throw new ArgumentException($"Unsupported header exists-action '{action}'.", nameof(header.ExistsAction));
            }

            var values = ValidateHeader(header.Name, action == "delete" ? [] : header.Values, allowEmpty: action == "delete");
            if (action == "delete")
            {
                target.Remove(header.Name);
                deleted.Add(header.Name);
                continue;
            }

            switch (action)
            {
                case "override":
                    target[header.Name] = values.ToArray();
                    break;
                case "append":
                    AppendHeader(target, header.Name, values);
                    break;
                case "skip":
                    target.TryAdd(header.Name, values.ToArray());
                    break;
            }

            deleted.Remove(header.Name);
        }

        return deleted;
    }

    private static void AppendHeader(Dictionary<string, string[]> target, string name, string[] values)
    {
        target[name] = target.TryGetValue(name, out var existing)
            ? existing.Concat(values).ToArray()
            : values.ToArray();
    }

    private static Encoding GetStringBodyEncoding(Dictionary<string, string[]> headers)
    {
        if (!headers.TryGetValue("Content-Type", out var values))
        {
            return Encoding.UTF8;
        }
        if (values.Length != 1 || !MediaTypeHeaderValue.TryParse(values[0], out var contentType))
        {
            throw new ArgumentException("Content-Type must contain one valid media type.", nameof(headers));
        }
        if (contentType.CharSet is null)
        {
            return Encoding.UTF8;
        }

        var charset = contentType.CharSet.Trim('"');
        try
        {
            return Encoding.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            throw new NotSupportedException($"HTTP string request body charset '{charset}' is not supported.", error);
        }
    }

    private static string[] ValidateHeader(string name, string[]? values, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(name) || !IsToken(name))
        {
            throw new ArgumentException("Header names must be nonempty HTTP tokens.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(values);
        if (!allowEmpty && values.Length == 0)
        {
            throw new ArgumentException("A header requires at least one value unless its exists-action is 'delete'.", nameof(values));
        }
        if (values.Any(value => value is null || value.Contains('\r') || value.Contains('\n')))
        {
            throw new ArgumentException("Header values must be non-null and cannot contain line breaks.", nameof(values));
        }

        return values;
    }

    private static bool IsToken(string value) =>
        value.All(character => char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));

    private static HttpTransportOptions ApplyAuthentication(
        GatewayContext context, HttpRequestMessage request, IAuthenticationConfig? config, HttpTransportOptions options)
    {
        switch (config)
        {
            case null:
                return options;
            case BasicAuthenticationConfig basic:
                ArgumentNullException.ThrowIfNull(basic.Username);
                ArgumentNullException.ThrowIfNull(basic.Password);
                if (basic.Username.Contains(':'))
                {
                    throw new ArgumentException("Basic authentication usernames cannot contain ':'.", nameof(basic.Username));
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{basic.Username}:{basic.Password}")));
                return options;
            case ManagedIdentityAuthenticationConfig identity:
                ApplyManagedIdentity(context, request, identity);
                return options;
            case CertificateAuthenticationConfig certificate:
                return options with { Certificate = ResolveCertificate(context, request, certificate) };
            default:
                throw new NotSupportedException($"HTTP request authentication '{config.GetType().Name}' is not supported.");
        }
    }

    private static void ApplyManagedIdentity(
        GatewayContext context, HttpRequestMessage request, ManagedIdentityAuthenticationConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Resource);
        if (config.ClientId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(config.ClientId);
        if (config.OutputTokenVariableName is not null) ArgumentException.ThrowIfNullOrWhiteSpace(config.OutputTokenVariableName);
        var provider = context.ManagedIdentityTokenProvider
            ?? throw new InvalidOperationException("HTTP managed identity authentication requires an injected ManagedIdentityTokenProvider.");
        string token;
        try
        {
            token = provider(config.Resource, config.ClientId);
        }
        catch (Exception error) when (config.IgnoreError == true
            && error is HttpRequestException or OperationCanceledException or IOException)
        {
            context.Trace($"HTTP managed identity authentication failed and was explicitly ignored: {error.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(token) || token.Contains('\r') || token.Contains('\n'))
        {
            throw new InvalidOperationException("The managed identity token provider returned an invalid or empty access token.");
        }

        if (config.OutputTokenVariableName is not null)
        {
            context.Variables[config.OutputTokenVariableName] = token;
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static X509Certificate2 ResolveCertificate(
        GatewayContext context, HttpRequestMessage request, CertificateAuthenticationConfig config)
    {
        var hasId = !string.IsNullOrWhiteSpace(config.CertificateId);
        var hasThumbprint = !string.IsNullOrWhiteSpace(config.Thumbprint);
        var sources = (hasId ? 1 : 0) + (hasThumbprint ? 1 : 0) + (config.Body is not null ? 1 : 0);
        if (sources != 1 || (config.Password is not null && config.Body is null))
        {
            throw new InvalidOperationException("HTTP client certificate authentication requires exactly one certificate source.");
        }

        if (hasId)
        {
            return context.CertificateStore.ById.GetValueOrDefault(config.CertificateId!)
                ?? throw new InvalidOperationException("The configured client certificate ID was not found.");
        }
        if (hasThumbprint)
        {
            return context.CertificateStore.ByThumbprint.GetValueOrDefault(config.Thumbprint!)
                ?? throw new InvalidOperationException("The configured client certificate thumbprint was not found.");
        }
        if (config.Body is not { Length: > 0 } body)
        {
            throw new CryptographicException("The client certificate body must not be empty.");
        }

        var certificate = new X509Certificate2(body, config.Password);
        request.Options.Set(s_ownedCertificateKey, certificate);
        return certificate;
    }
}