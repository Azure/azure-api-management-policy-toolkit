// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRemoveQueryParameterProvider
{
    public static Setup RemoveQueryParameter(this MockPoliciesProvider<IInboundContext> mock) =>
        RemoveQueryParameter(mock, (_, _) => true);

    public static Setup RemoveQueryParameter(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<RemoveQueryParameterHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool> _predicate;
        private readonly RemoveQueryParameterHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool> predicate,
            RemoveQueryParameterHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}