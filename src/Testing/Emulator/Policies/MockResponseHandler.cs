// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class MockResponseHandler : PolicyHandlerOptionalParam<MockResponseConfig>
{
    public override string PolicyName => nameof(IInboundContext.MockResponse);

    protected override void Handle(GatewayContext context, MockResponseConfig? config)
    {
        throw new NotImplementedException();
    }
}