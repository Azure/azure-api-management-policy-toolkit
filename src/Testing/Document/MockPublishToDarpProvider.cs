// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockPublishToDarpProvider
{
    public static Setup PublishToDarp<T>(this MockPoliciesProvider<T> mock) where T : class =>
        PublishToDarp(mock, (_, _) => true);

    public static Setup PublishToDarp<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, PublishToDarpConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<PublishToDarpHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, PublishToDarpConfig, bool> _predicate;
        private readonly PublishToDarpHandler _handler;

        internal Setup(
            Func<GatewayContext, PublishToDarpConfig, bool> predicate,
            PublishToDarpHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, PublishToDarpConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}
