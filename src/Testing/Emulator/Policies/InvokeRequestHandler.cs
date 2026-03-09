// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class InvokeRequestHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, InvokeRequestConfig, bool>,
        Action<GatewayContext, InvokeRequestConfig>
    >> CallbackHooks { get; } = new();

    public string PolicyName => nameof(IInboundContext.InvokeRequest);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractArgument<InvokeRequestConfig>();
        var callbackHook = CallbackHooks.Find(hook => hook.Item1(context, config));
        if (callbackHook is not null)
        {
            callbackHook.Item2(context, config);
        }
        else
        {
            Handle(context, config);
        }

        if (string.IsNullOrWhiteSpace(config.ResponseVariableName))
        {
            throw new FinishSectionProcessingException();
        }

        return null;
    }

    private static void Handle(GatewayContext context, InvokeRequestConfig config)
    {
        var httpClient = context.Services.Resolve<IHttpClient>();
        if (httpClient is null)
        {
            return;
        }

        var url = config.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = context.BackendUrl is not null
                ? context.BackendUrl.TrimEnd('/') + context.Request.Url.Path + context.Request.Url.QueryString
                : context.Request.Url.ToString();
        }

        using var request = new HttpRequestMessage(
            new HttpMethod(config.Method ?? context.Request.Method),
            url);

        foreach (var header in context.Request.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (config.Headers is not null)
        {
            foreach (var header in config.Headers)
            {
                if (header.Values is null)
                {
                    continue;
                }

                request.Headers.Remove(header.Name);
                request.Headers.TryAddWithoutValidation(header.Name, header.Values);
            }
        }

        var bodyContent = config.Body?.Content?.ToString();
        if (string.IsNullOrEmpty(bodyContent) && context.Request.Body?.Content is not null)
        {
            bodyContent = context.Request.Body.As<string>(preserveContent: true);
        }

        if (!string.IsNullOrEmpty(bodyContent))
        {
            request.Content = new StringContent(bodyContent);
        }

        using var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        var mockResponse = ToMockResponse(response);

        if (!string.IsNullOrWhiteSpace(config.ResponseVariableName))
        {
            context.Variables[config.ResponseVariableName] = mockResponse;
        }
        else
        {
            CopyResponse(mockResponse, context.Response);
        }
    }

    private static void CopyResponse(MockResponse source, MockResponse target)
    {
        target.StatusCode = source.StatusCode;
        target.StatusReason = source.StatusReason;
        target.Headers.Clear();
        foreach (var header in source.Headers)
        {
            target.Headers[header.Key] = header.Value;
        }

        target.Body.Content = source.Body.Content;
    }

    private static MockResponse ToMockResponse(HttpResponseMessage response)
    {
        var mockResponse = new MockResponse
        {
            StatusCode = (int)response.StatusCode,
            StatusReason = response.ReasonPhrase ?? string.Empty,
        };

        foreach (var header in response.Headers)
        {
            mockResponse.Headers[header.Key] = header.Value.ToArray();
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                mockResponse.Headers[header.Key] = header.Value.ToArray();
            }

            mockResponse.Body.Content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        return mockResponse;
    }
}
