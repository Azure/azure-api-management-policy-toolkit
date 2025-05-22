// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRateLimitByKeyProvider
{
    public static Setup RateLimitByKey(this MockPoliciesProvider<IInboundContext> mock) =>
        RateLimitByKey(mock, (_, _) => true);

    public static Setup RateLimitByKey(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, RateLimitByKeyConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<RateLimitByKeyHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, RateLimitByKeyConfig, bool> _predicate;
        private readonly RateLimitByKeyHandler _handler;

        internal Setup(
            Func<GatewayContext, RateLimitByKeyConfig, bool> predicate,
            RateLimitByKeyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, RateLimitByKeyConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}