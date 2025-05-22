// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

public class TestDocument(IDocument document)
{
    public GatewayContext Context { get; init; } = new();

    public void RunInbound() => this.Handle(Context.InboundProxy.Object, document.Inbound);
    public void RunBackend() => this.Handle(Context.BackendProxy.Object, document.Backend);
    public void RunOutbound() => this.Handle(Context.OutboundProxy.Object, document.Outbound);
    public void RunOnError() => this.Handle(Context.OnErrorProxy.Object, document.OnError);

    private void Handle<T>(T context, Action<T> section)
    {
        try
        {
            section(context);
        }
        catch (FinishSectionProcessingException) { }
    }
}