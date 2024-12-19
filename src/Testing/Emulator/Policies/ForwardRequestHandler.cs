﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IBackendContext))]
internal class ForwardRequestHandler : PolicyHandlerOptionalParam<ForwardRequestConfig>
{
    public override string PolicyName => nameof(IBackendContext.ForwardRequest);

    protected override void Handle(GatewayContext context, ForwardRequestConfig? config)
    {
        throw new NotImplementedException();
    }
}