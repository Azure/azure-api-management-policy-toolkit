// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockLogToEventHubProvider
{
    public static Setup LogToEventHub<T>(this MockPoliciesProvider<T> mock) where T : class =>
        LogToEventHub(mock, (_, _) => true);

    public static Setup LogToEventHub<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, LogToEventHubConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<LogToEventHubHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, LogToEventHubConfig, bool> _predicate;
        private readonly LogToEventHubHandler _handler;

        internal Setup(
            Func<GatewayContext, LogToEventHubConfig, bool> predicate,
            LogToEventHubHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, LogToEventHubConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}