// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRateLimitProvider
{
    public static Setup RateLimit(this MockPoliciesProvider<IInboundContext> mock) =>
        RateLimit(mock, (_, _) => true);

    public static Setup RateLimit(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, RateLimitConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<RateLimitHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, RateLimitConfig, bool> _predicate;
        private readonly RateLimitHandler _handler;

        internal Setup(
            Func<GatewayContext, RateLimitConfig, bool> predicate,
            RateLimitHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, RateLimitConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}