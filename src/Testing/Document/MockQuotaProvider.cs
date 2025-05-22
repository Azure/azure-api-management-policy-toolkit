// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockQuotaProvider
{
    public static Setup Quota(this MockPoliciesProvider<IInboundContext> mock) =>
        Quota(mock, (_, _) => true);

    public static Setup Quota(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, QuotaConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<QuotaHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, QuotaConfig, bool> _predicate;
        private readonly QuotaHandler _handler;

        internal Setup(
            Func<GatewayContext, QuotaConfig, bool> predicate,
            QuotaHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, QuotaConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}