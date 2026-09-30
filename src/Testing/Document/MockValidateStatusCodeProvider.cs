// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateStatusCodeProvider
{
    public static Setup ValidateStatusCode(this MockPoliciesProvider<IOutboundContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateStatusCode(this MockPoliciesProvider<IOnErrorContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateStatusCode(this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, ValidateStatusCodeConfig, bool> predicate) => Create(mock, predicate);

    public static Setup ValidateStatusCode(this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, ValidateStatusCodeConfig, bool> predicate) => Create(mock, predicate);

    private static Setup Create<T>(MockPoliciesProvider<T> mock,
        Func<GatewayContext, ValidateStatusCodeConfig, bool> predicate) where T : class =>
        new(predicate, mock.SectionContextProxy.GetHandler<ValidateStatusCodeHandler>());

    public sealed class Setup
    {
        private readonly Func<GatewayContext, ValidateStatusCodeConfig, bool> _predicate;
        private readonly ValidateStatusCodeHandler _handler;

        internal Setup(Func<GatewayContext, ValidateStatusCodeConfig, bool> predicate,
            ValidateStatusCodeHandler handler)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateStatusCodeConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }
    }
}