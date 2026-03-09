// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IBackendContext))]
internal class ForwardRequestHandler : PolicyHandlerOptionalParam<ForwardRequestConfig>
{
    public override string PolicyName => nameof(IBackendContext.ForwardRequest);

    protected override void Handle(GatewayContext context, ForwardRequestConfig? config)
    {
        var httpClient = context.Services.Resolve<IHttpClient>();
        if (httpClient is null)
        {
            // No-op: in test mode without an HTTP client, leave response as-is
            return;
        }

        var url = context.BackendUrl is not null
            ? context.BackendUrl.TrimEnd('/') + context.Request.Url.Path + context.Request.Url.QueryString
            : context.Request.Url.ToString();
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), url);

        // Copy request headers
        foreach (var header in context.Request.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy body
        var bodyContent = context.Request.Body?.As<string>(preserveContent: true);
        if (!string.IsNullOrEmpty(bodyContent))
        {
            request.Content = new StringContent(bodyContent);
        }

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();

        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.StatusReason = response.ReasonPhrase ?? "";

        // Copy response headers
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            context.Response.Body.Content = content;
        }
    }
}