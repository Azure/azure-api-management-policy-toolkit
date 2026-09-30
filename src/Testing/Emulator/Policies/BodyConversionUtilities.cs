// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

internal static class BodyConversionUtilities
{
    public static MockMessage Message<THandler>(GatewayContext context, THandler handler)
        where THandler : class, IPolicyHandler
    {
        // Proxies own distinct instances, including those shared with fragments and callback providers.
        if (ReferenceEquals(handler, context.InboundProxy.GetHandler<THandler>())
            || ReferenceEquals(handler, context.BackendProxy.GetHandler<THandler>()))
        {
            return context.Request;
        }

        if (ReferenceEquals(handler, context.OutboundProxy.GetHandler<THandler>())
            || ReferenceEquals(handler, context.OnErrorProxy.GetHandler<THandler>()))
        {
            return context.Response;
        }

        throw new InvalidOperationException("The body conversion handler does not belong to this gateway context.");
    }

    public static void ValidateApply(string apply, string source)
    {
        if (apply != "always" && apply != $"content-type-{source}")
        {
            throw new ArgumentException($"Apply must be 'always' or 'content-type-{source}'.", nameof(apply));
        }
    }

    public static bool ShouldApply(
        GatewayContext context,
        MockMessage message,
        string apply,
        bool? considerAccept,
        string source,
        string destination)
    {
        if (apply != "always" && !HasContentType(message, source))
        {
            return false;
        }

        if (considerAccept == false)
        {
            return true;
        }

        using var request = new HttpRequestMessage();
        foreach (var value in HeaderValues(context.Request, "Accept"))
        {
            if (!string.IsNullOrWhiteSpace(value) && !request.Headers.Accept.TryParseAdd(value))
            {
                throw new FormatException($"Invalid Accept header value '{value}'.");
            }
        }

        return request.Headers.Accept.Any(value =>
            value.Quality.GetValueOrDefault(1) > 0 && IsMediaType(value.MediaType, destination));
    }

    public static string Body(MockMessage message, string policy)
    {
        if (string.IsNullOrWhiteSpace(message.Body.Content))
        {
            throw new InvalidOperationException($"{policy} requires a non-empty message body.");
        }

        return message.Body.Content;
    }

    public static void ReplaceBody(MockMessage message, string body, string contentType)
    {
        var keys = message.Headers.Keys
            .Where(key => key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var contentLength = keys.Any(key => key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            ? Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture)
            : null;
        foreach (var key in keys)
        {
            message.Headers.Remove(key);
        }

        message.Body.Content = body;
        message.Headers["Content-Type"] = [contentType];
        if (contentLength is not null)
        {
            message.Headers["Content-Length"] = [contentLength];
        }
    }

    private static bool HasContentType(MockMessage message, string source)
    {
        var matches = false;
        foreach (var value in HeaderValues(message, "Content-Type"))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!MediaTypeHeaderValue.TryParse(value, out var parsed))
            {
                throw new FormatException($"Invalid Content-Type header value '{value}'.");
            }

            matches |= IsMediaType(parsed.MediaType, source);
        }

        return matches;
    }

    private static bool IsMediaType(string? mediaType, string subtype) =>
        mediaType is not null
        && !mediaType.Contains('*')
        && (mediaType.Equals($"application/{subtype}", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals($"text/{subtype}", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith($"+{subtype}", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> HeaderValues(MockMessage message, string name) =>
        message.Headers
            .Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value);
}
