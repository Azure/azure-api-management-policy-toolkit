// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockLimitConcurrencyProvider
{
    public static Setup LimitConcurrency<T>(this MockPoliciesProvider<T> mock) where T : class =>
        LimitConcurrency(mock, (_, _, _) => true);

    public static Setup LimitConcurrency<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, LimitConcurrencyConfig, Action, bool> predicate) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<LimitConcurrencyHandler>();
        return new Setup(predicate, handler);
    }

    public sealed class Setup
    {
        private readonly Func<GatewayContext, LimitConcurrencyConfig, Action, bool> _predicate;
        private readonly LimitConcurrencyHandler _handler;

        internal Setup(
            Func<GatewayContext, LimitConcurrencyConfig, Action, bool> predicate,
            LimitConcurrencyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, LimitConcurrencyConfig, Action> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}