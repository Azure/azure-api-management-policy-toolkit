// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Context interface for policy fragments that combines all policy capabilities.
/// Fragments can contain any policy elements that are valid in inbound, outbound, backend, or on-error sections.
/// </summary>
public interface IFragmentContext : IInboundContext, IOutboundContext, IBackendContext, IOnErrorContext
{
}
