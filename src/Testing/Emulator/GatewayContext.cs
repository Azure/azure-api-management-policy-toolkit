﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

public class GatewayContext : MockExpressionContext
{
    internal readonly SectionContextProxy<IInboundContext> InboundProxy;
    internal readonly SectionContextProxy<IBackendContext> BackendProxy;
    internal readonly SectionContextProxy<IOutboundContext> OutboundProxy;
    internal readonly SectionContextProxy<IOnErrorContext> OnErrorProxy;

    public GatewayContext()
    {
        InboundProxy = SectionContextProxy<IInboundContext>.Create(this);
        BackendProxy = SectionContextProxy<IBackendContext>.Create(this);
        OutboundProxy = SectionContextProxy<IOutboundContext>.Create(this);
        OnErrorProxy = SectionContextProxy<IOnErrorContext>.Create(this);
    }
}