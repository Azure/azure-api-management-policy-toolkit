// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class TestDocumentExtensions
{
    public static MockPoliciesProvider<IInboundContext> SetupInbound(this TestDocument document) =>
        new(document.Context.InboundProxy);

    public static MockPoliciesProvider<IBackendContext> SetupBackend(this TestDocument document) =>
        new(document.Context.BackendProxy);

    public static MockPoliciesProvider<IOutboundContext> SetupOutbound(this TestDocument document) =>
        new(document.Context.OutboundProxy);

    public static MockPoliciesProvider<IOnErrorContext> SetupOnError(this TestDocument document) =>
        new(document.Context.OnErrorProxy);

    public static CertificateStore SetupCertificateStore(this TestDocument document) =>
        document.Context.CertificateStore;

    public static CacheStore SetupCacheStore(this TestDocument document) =>
        document.Context.CacheStore;

    public static CacheInfo SetupCacheInfo(this TestDocument document) =>
        document.Context.CacheInfo;
}