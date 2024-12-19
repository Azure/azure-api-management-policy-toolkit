﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockSetQueryParameterIfNotExistProvider
{
    public static Setup SetQueryParameterIfNotExist(this MockPoliciesProvider<IInboundContext> mock) =>
        SetQueryParameterIfNotExist(mock, (_, _, _) => true);

    public static Setup SetQueryParameterIfNotExist(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, string, string[], bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<SetQueryParameterIfNotExistHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, string, string[], bool> _predicate;
        private readonly SetQueryParameterIfNotExistHandler _handler;

        internal Setup(
            Func<GatewayContext, string, string[], bool> predicate,
            SetQueryParameterIfNotExistHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, string, string[]> callback) =>
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
    }
}