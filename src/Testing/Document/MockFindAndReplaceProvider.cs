// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockFindAndReplaceProvider
{
    public static Setup FindAndReplace(this MockPoliciesProvider<IInboundContext> mock) =>
        FindAndReplace(mock, (_, _, _) => true);

    public static Setup FindAndReplace(this MockPoliciesProvider<IOutboundContext> mock) =>
        FindAndReplace(mock, (_, _, _) => true);

    public static Setup FindAndReplace(this MockPoliciesProvider<IBackendContext> mock) =>
        FindAndReplace(mock, (_, _, _) => true);

    public static Setup FindAndReplace(this MockPoliciesProvider<IOnErrorContext> mock) =>
        FindAndReplace(mock, (_, _, _) => true);

    public static Setup FindAndReplace(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string, bool> predicate
    ) => FindAndReplace<IInboundContext, FindAndReplaceRequestHandler>(mock, predicate);

    public static Setup FindAndReplace(
        this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, string, string, bool> predicate
    ) => FindAndReplace<IOutboundContext, FindAndReplaceResponseHandler>(mock, predicate);

    public static Setup FindAndReplace(
        this MockPoliciesProvider<IBackendContext> mock,
        Func<GatewayContext, string, string, bool> predicate
    ) => FindAndReplace<IBackendContext, FindAndReplaceRequestHandler>(mock, predicate);

    public static Setup FindAndReplace(
        this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, string, string, bool> predicate
    ) => FindAndReplace<IOnErrorContext, FindAndReplaceResponseHandler>(mock, predicate);

    private static Setup FindAndReplace<TContext, THandler>(
        MockPoliciesProvider<TContext> mock,
        Func<GatewayContext, string, string, bool> predicate
    )
        where TContext : class
        where THandler : FindAndReplaceHandler
    {
        var handler = mock.SectionContextProxy.GetHandler<THandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string, bool> _predicate;
        private readonly FindAndReplaceHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string, bool> predicate,
            FindAndReplaceHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
