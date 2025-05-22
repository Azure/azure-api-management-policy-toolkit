// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetVariableProvider
{
    public static Setup SetVariable<T>(this MockPoliciesProvider<T> mock) where T : class =>
        SetVariable(mock, (_, _, _) => true);

    public static Setup SetVariable<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, string, object, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SetVariableHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, object, bool> _predicate;
        private readonly SetVariableHandler _handler;

        internal Setup(
            Func<GatewayContext, string, object, bool> predicate,
            SetVariableHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, object> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}