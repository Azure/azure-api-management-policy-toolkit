// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockInlineProvider
{
    public static Setup Inline<T>(this MockPoliciesProvider<T> mock) where T : class =>
        Inline(mock, (_, _) => true);

    public static Setup Inline<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, string, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<InlinePolicyHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool> _predicate;
        private readonly InlinePolicyHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool> predicate,
            InlinePolicyHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}