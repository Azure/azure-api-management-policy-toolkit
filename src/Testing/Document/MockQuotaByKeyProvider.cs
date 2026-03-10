// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockQuotaByKeyProvider
{
    public static Setup QuotaByKey(this MockPoliciesProvider<IInboundContext> mock) =>
        QuotaByKey(mock, (_, _) => true);

    public static Setup QuotaByKey(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, QuotaByKeyConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<QuotaByKeyHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, QuotaByKeyConfig, bool> _predicate;
        private readonly QuotaByKeyHandler _handler;

        internal Setup(
            Func<GatewayContext, QuotaByKeyConfig, bool> predicate,
            QuotaByKeyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, QuotaByKeyConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
