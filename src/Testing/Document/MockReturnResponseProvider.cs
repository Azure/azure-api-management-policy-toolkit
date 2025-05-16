// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockReturnResponseProvider
{
    public static Setup ReturnResponse(this MockPoliciesProvider<IBackendContext> mock) =>
        ReturnResponse(mock, (_, _) => true);

    public static Setup ReturnResponse<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, ReturnResponseConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<ReturnResponseHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ReturnResponseConfig, bool> _predicate;
        private readonly ReturnResponseHandler _handler;

        internal Setup(
            Func<GatewayContext, ReturnResponseConfig, bool> predicate,
            ReturnResponseHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ReturnResponseConfig> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}