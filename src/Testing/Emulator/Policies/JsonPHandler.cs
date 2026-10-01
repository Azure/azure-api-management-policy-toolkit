// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IOutboundContext))]
internal class JsonPHandler : PolicyHandler<string>
{
    private const string SafeJsonContentType = "application/javascript";
    private const string ErrorJsonContentType = "application/json";
    private static readonly Regex s_safeCallbackPattern = new(@"\A[A-Za-z_$][A-Za-z0-9_$]*(?:\.[A-Za-z_$][A-Za-z0-9_$]*)*\z", RegexOptions.Compiled);

    public override string PolicyName => nameof(IOutboundContext.JsonP);

    protected override void Handle(GatewayContext context, string callbackParameterName)
    {
        if (string.IsNullOrWhiteSpace(callbackParameterName))
        {
            throw new ArgumentException("The callback parameter name must be a non-empty value.", nameof(callbackParameterName));
        }

        if (!TryGetCallbackValue(context, callbackParameterName, out var callbackValue, out var callbackFound))
        {
            if (!callbackFound)
            {
                return;
            }

            Fail(context, $"The '{callbackParameterName}' callback parameter value is invalid.");
            return;
        }

        if (!IsSafeCallback(callbackValue))
        {
            Fail(context, $"The '{callbackParameterName}' callback parameter value is invalid.");
            return;
        }

        var responseBody = context.Response.Body.Content;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            Fail(context, "The response body is not valid JSON.");
            return;
        }

        try
        {
            JToken.Parse(responseBody);
        }
        catch (JsonReaderException)
        {
            // APIM docs do not specify a fallback for malformed JSON; we conservatively reject it.
            Fail(context, "The response body is not valid JSON.");
            return;
        }

        var wrappedBody = $"{callbackValue}({responseBody});";
        context.Response.Body.Content = wrappedBody;
        SetContentHeaders(context.Response, SafeJsonContentType, wrappedBody);
    }

    private static bool IsSafeCallback(string callbackValue) =>
        !string.IsNullOrWhiteSpace(callbackValue) && s_safeCallbackPattern.IsMatch(callbackValue);

    private static bool TryGetCallbackValue(
        GatewayContext context,
        string callbackParameterName,
        out string callbackValue,
        out bool callbackFound)
    {
        callbackValue = string.Empty;
        callbackFound = false;

        foreach (var queryPair in context.Request.Url.Query)
        {
            if (!string.Equals(queryPair.Key, callbackParameterName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            callbackFound = true;
            callbackValue = queryPair.Value.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(callbackValue);
        }

        return false;
    }

    private static void Fail(GatewayContext context, string message)
    {
        var response = context.Response;
        response.StatusCode = 400;
        response.StatusReason = "Bad Request";
        var errorBody = JObject.FromObject(new { error = message }).ToString(Formatting.None);
        response.Body.Content = errorBody;
        SetContentHeaders(response, ErrorJsonContentType, errorBody);
        throw new FinishSectionProcessingException();
    }

    private static void SetContentHeaders(MockMessage response, string contentType, string body)
    {
        var keys = response.Headers.Keys
            .Where(key => key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var key in keys)
        {
            response.Headers.Remove(key);
        }

        response.Headers["Content-Type"] = [contentType];
        response.Headers["Content-Length"] = [Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture)];
    }
}