// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetStatusProvider
{
    public static Setup SetStatus<T>(this MockPoliciesProvider<T> mock) where T : class =>
        SetStatus(mock, (_, _) => true);

    public static Setup SetStatus<T>(this MockPoliciesProvider<T> mock, int statusCode)
        where T : class =>
        SetStatus(mock, (_, config) => config.Code == statusCode);

    public static Setup SetStatus<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, StatusConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<SetStatusHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, StatusConfig, bool> _predicate;
        private readonly SetStatusHandler _handler;

        internal Setup(
            Func<GatewayContext, StatusConfig, bool> predicate,
            SetStatusHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, StatusConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}