// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateHeadersProvider
{
    public static Setup ValidateHeaders(this MockPoliciesProvider<IOutboundContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateHeaders(this MockPoliciesProvider<IOnErrorContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateHeaders(this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, ValidateHeadersConfig, bool> predicate) => Create(mock, predicate);

    public static Setup ValidateHeaders(this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, ValidateHeadersConfig, bool> predicate) => Create(mock, predicate);

    private static Setup Create<T>(MockPoliciesProvider<T> mock,
        Func<GatewayContext, ValidateHeadersConfig, bool> predicate) where T : class =>
        new(predicate, mock.SectionContextProxy.GetHandler<ValidateHeadersHandler>());

    public sealed class Setup
    {
        private readonly Func<GatewayContext, ValidateHeadersConfig, bool> _predicate;
        private readonly ValidateHeadersHandler _handler;

        internal Setup(Func<GatewayContext, ValidateHeadersConfig, bool> predicate, ValidateHeadersHandler handler)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateHeadersConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }
    }
}