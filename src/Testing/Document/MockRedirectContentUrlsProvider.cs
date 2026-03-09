// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockRedirectContentUrlsProvider
{
    public static Setup RedirectContentUrls(this MockPoliciesProvider<IOutboundContext> mock) =>
        RedirectContentUrls(mock, _ => true);

    public static Setup RedirectContentUrls(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<RedirectContentUrlsHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, bool> _predicate;
        private readonly RedirectContentUrlsHandler _handler;

        internal Setup(
            Func<GatewayContext, bool> predicate,
            RedirectContentUrlsHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}
