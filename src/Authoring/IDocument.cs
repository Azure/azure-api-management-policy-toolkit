// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

public interface IDocument
{
    void Inbound(IInboundContext context) { }
    void Outbound(IOutboundContext context) { }
    void Backend(IBackendContext context) { }
    void OnError(IOnErrorContext context) { }
}