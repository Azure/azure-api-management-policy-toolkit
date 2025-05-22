// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockBaseProvider
{
    public static Setup Base<T>(this MockPoliciesProvider<T> mock) where T : class => Base(mock, _ => true);

    public static Setup Base<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<BaseHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, bool> _predicate;
        private readonly BaseHandler _handler;

        internal Setup(
            Func<GatewayContext, bool> predicate,
            BaseHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}