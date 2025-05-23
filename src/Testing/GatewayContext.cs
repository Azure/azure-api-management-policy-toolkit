// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

public class GatewayContext : MockExpressionContext
{
    internal readonly SectionContextProxy<IInboundContext> InboundProxy;
    internal readonly SectionContextProxy<IBackendContext> BackendProxy;
    internal readonly SectionContextProxy<IOutboundContext> OutboundProxy;
    internal readonly SectionContextProxy<IOnErrorContext> OnErrorProxy;
    internal readonly CertificateStore CertificateStore = new();
    internal readonly CacheStore CacheStore = new();
    internal readonly CacheInfo CacheInfo = new();

    public GatewayContext()
    {
        InboundProxy = SectionContextProxy<IInboundContext>.Create(this);
        BackendProxy = SectionContextProxy<IBackendContext>.Create(this);
        OutboundProxy = SectionContextProxy<IOutboundContext>.Create(this);
        OnErrorProxy = SectionContextProxy<IOnErrorContext>.Create(this);
    }
}