// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IBackendContext))]
internal class ForwardRequestHandler : PolicyHandlerOptionalParam<ForwardRequestConfig>
{
    public override string PolicyName => nameof(IBackendContext.ForwardRequest);

    protected override void Handle(GatewayContext context, ForwardRequestConfig? config)
    {
        var mockResponse = context.ForwardRequestStore.GetNext();

        if (mockResponse is null)
        {
            return;
        }

        context.Response = new MockResponse
        {
            StatusCode = mockResponse.StatusCode,
            StatusReason = mockResponse.StatusReason,
        };

        foreach (var (key, values) in mockResponse.Headers)
        {
            context.Response.Headers[key] = values;
        }

        if (mockResponse.Body is not null)
        {
            context.Response.Body.Content = mockResponse.Body;
        }
    }
}