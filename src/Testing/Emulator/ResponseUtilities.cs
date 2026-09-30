// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal static class ResponseUtilities
{
    public static void Overwrite(MockResponse target, int statusCode, string? statusReason = null)
    {
        target.StatusCode = statusCode;
        target.StatusReason = statusReason ?? target.StatusReason;
        target.Headers.Clear();
        target.Body.Content = string.Empty;
    }

    public static void Copy(IResponse source, MockResponse target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(source, target))
        {
            return;
        }

        var statusCode = source.StatusCode;
        var statusReason = source.StatusReason;
        var headers = source.Headers.Select(header =>
            new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToArray();
        var body = source is MockResponse response
            ? response.Body.Content
            : source.Body.As<string>(preserveContent: true);

        target.StatusCode = statusCode;
        target.StatusReason = statusReason;
        target.Headers.Clear();
        foreach (var header in headers)
        {
            target.Headers[header.Key] = header.Value;
        }

        target.Body.Content = body;
    }

    public static bool TryCopyCachedResponse(object? cached, MockResponse target)
    {
        switch (cached)
        {
            case null:
                return false;
            case MockResponse response:
                Copy(response, target);
                return true;
            case CachedResponse response:
                target.StatusCode = response.StatusCode;
                target.StatusReason = response.StatusReason ?? string.Empty;
                target.Headers.Clear();
                foreach (var header in response.Headers)
                {
                    target.Headers[header.Key] = header.Value.ToArray();
                }

                target.Body.Content = response.Body;
                return true;
            default:
                return TryCopyResponseLike(cached, target);
        }
    }

    private static bool TryCopyResponseLike(object cached, MockResponse target)
    {
        var type = cached.GetType();
        var statusCode = type.GetProperty("StatusCode", BindingFlags.Instance | BindingFlags.Public);
        var headers = type.GetProperty("Headers", BindingFlags.Instance | BindingFlags.Public);
        if (statusCode?.GetValue(cached) is not int code
            || headers?.GetValue(cached) is not IReadOnlyDictionary<string, string[]> responseHeaders)
        {
            return false;
        }

        target.StatusCode = code;
        target.StatusReason = type.GetProperty("StatusReason", BindingFlags.Instance | BindingFlags.Public)?.GetValue(cached) as string ?? string.Empty;
        target.Headers.Clear();
        foreach (var header in responseHeaders)
        {
            target.Headers[header.Key] = header.Value.ToArray();
        }

        target.Body.Content = type.GetProperty("Body", BindingFlags.Instance | BindingFlags.Public)?.GetValue(cached) as string;
        return true;
    }
}