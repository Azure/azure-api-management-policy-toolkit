// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class SendOneWayRequestHandler : PolicyHandler<SendOneWayRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.SendOneWayRequest);

    protected override void Handle(GatewayContext context, SendOneWayRequestConfig config)
    {
        var httpClient = context.Services.Resolve<IHttpClient>();
        if (httpClient is null)
        {
            // Fire-and-forget: silently skip if no HTTP client registered
            return;
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

        // Fire-and-forget: send but don't wait for or process the response
        _ = httpClient.SendAsync(request).GetAwaiter().GetResult();
    }
}
