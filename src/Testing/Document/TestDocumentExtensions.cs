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

    public static ResponseExampleStore SetupResponseExampleStore(this TestDocument document) =>
        document.Context.ResponseExampleStore;

    public static LoggerStore SetupLoggerStore(this TestDocument document) =>
        document.Context.LoggerStore;

    public static MetricStore SetupMetricStore(this TestDocument document) =>
        document.Context.MetricStore;

    public static RateLimitStore SetupRateLimitStore(this TestDocument document) =>
        document.Context.RateLimitStore;
}