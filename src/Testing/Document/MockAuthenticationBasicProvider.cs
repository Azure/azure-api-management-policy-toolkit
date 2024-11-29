﻿using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Document;

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
        AuthenticationBasic(mock, (_, actualUser, actualName) => actualUser == username && actualName == password);

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
            _handler.CallbackHooks.Add(
                new Tuple<Func<GatewayContext, string, string, bool>, Action<GatewayContext, string, string>>(
                    _predicate, callback));
    }
}