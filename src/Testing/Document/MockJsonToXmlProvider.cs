// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockJsonToXmlProvider
{
    public static Setup JsonToXml<T>(this MockPoliciesProvider<T> mock) where T : class =>
        JsonToXml(mock, (_, _) => true);

    public static Setup JsonToXml<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, JsonToXmlConfig, bool> predicate
    ) where T : class
    {
        var handler = mock.SectionContextProxy.GetHandler<JsonToXmlHandle>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, JsonToXmlConfig, bool> _predicate;
        private readonly JsonToXmlHandle _handler;

        internal Setup(
            Func<GatewayContext, JsonToXmlConfig, bool> predicate,
            JsonToXmlHandle handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, JsonToXmlConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}