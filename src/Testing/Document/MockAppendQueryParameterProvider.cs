﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockAppendQueryParameterProvider
{
    public static Setup AppendQueryParameter(this MockPoliciesProvider<IInboundContext> mock) =>
        AppendQueryParameter(mock, (_, _, _) => true);

    public static Setup AppendQueryParameter(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<AppendQueryParameterHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly AppendQueryParameterHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            AppendQueryParameterHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}