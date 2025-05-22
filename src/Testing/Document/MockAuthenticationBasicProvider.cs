// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAuthenticationBasicProvider
{
    public static Setup AuthenticationBasic(this MockPoliciesProvider<IInboundContext> mock) =>
        AuthenticationBasic(mock, (_, _, _) => true);

    public static Setup AuthenticationBasic(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<string, string, bool> predicate) =>
        AuthenticationBasic(mock, (_, username, password) => predicate(username, password));

    public static Setup AuthenticationBasic(
        this MockPoliciesProvider<IInboundContext> mock,
        string username,
        string password) =>
        AuthenticationBasic(mock,
            (_, actualUser, actualPassword) =>
                string.Equals(actualUser, username) && string.Equals(actualPassword, password));

    public static Setup AuthenticationBasic(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string, bool> predicate)
    {
        var handler = mock.SectionContextProxy.GetHandler<AuthenticationBasicHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string, bool> _predicate;
        private readonly AuthenticationBasicHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string, bool> predicate,
            AuthenticationBasicHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}