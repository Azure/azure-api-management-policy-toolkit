// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRewriteUriProvider
{
    public static Setup RewriteUri(this MockPoliciesProvider<IInboundContext> mock) =>
        RewriteUri(mock, (_, _, _) => true);

    public static Setup RewriteUri(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, bool, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<RewriteUriHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, bool, bool> _predicate;
        private readonly RewriteUriHandler _handler;

        internal Setup(
            Func<GatewayContext, string, bool, bool> predicate,
            RewriteUriHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, bool> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}