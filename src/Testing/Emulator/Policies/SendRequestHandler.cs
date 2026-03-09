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
internal class SendRequestHandler : PolicyHandler<SendRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendRequest);

    protected override void Handle(GatewayContext context, SendRequestConfig config)
    {
        var httpClient = context.Services.Resolve<IHttpClient>();
        if (httpClient is null)
        {
            if (config.IgnoreError == true)
            {
                if (config.ResponseVariableName is not null)
                {
                    context.Variables[config.ResponseVariableName] = null!;
                }

                return;
            }

            throw new InvalidOperationException(
                "No IHttpClient registered. Register one via test.Context.Services.Register<IHttpClient>(stub) " +
                "or use StubHttpClient.Ok() to provide a default response.");
        }

        HttpRequestMessage request;
        if (string.Equals(config.Mode, "copy", StringComparison.OrdinalIgnoreCase))
        {
            request = new HttpRequestMessage(
                new HttpMethod(config.Method ?? context.Request.Method),
                config.Url ?? context.Request.Url.ToString());

            foreach (var header in context.Request.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var bodyContent = context.Request.Body?.As<string>(preserveContent: true);
            if (!string.IsNullOrEmpty(bodyContent) && config.Body is null)
            {
                request.Content = new StringContent(bodyContent);
            }
        }
        else
        {
            request = new HttpRequestMessage(
                new HttpMethod(config.Method ?? "GET"),
                config.Url ?? context.Request.Url.ToString());
        }

        if (config.Headers is not null)
        {
            foreach (var header in config.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Name, header.Values);
            }
        }

        if (config.Body is not null)
        {
            request.Content = new StringContent(config.Body.Content?.ToString() ?? "");
        }

        HttpResponseMessage response;
        try
        {
            response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        }
        catch
        {
            if (config.IgnoreError == true)
            {
                // Store null to signal "no response" — policy code checks for null
                // and treats it as no response (e.g., StatusCode → -1)
                if (config.ResponseVariableName is not null)
                {
                    context.Variables[config.ResponseVariableName] = null!;
                }

                return;
            }

            throw;
        }

        if (config.ResponseVariableName is not null)
        {
            var mockResponse = new MockResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusReason = response.ReasonPhrase ?? ""
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

                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                mockResponse.Body.Content = content;
            }

            context.Variables[config.ResponseVariableName] = mockResponse;
        }
    }
}