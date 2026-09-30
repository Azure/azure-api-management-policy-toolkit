// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateParametersProvider
{
    public static Setup ValidateParameters(this MockPoliciesProvider<IInboundContext> mock) =>
        mock.ValidateParameters((_, _) => true);

    public static Setup ValidateParameters(this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ValidateParametersConfig, bool> predicate) =>
        new(predicate, mock.SectionContextProxy.GetHandler<ValidateParametersHandler>());

    public sealed class Setup
    {
        private readonly Func<GatewayContext, ValidateParametersConfig, bool> _predicate;
        private readonly ValidateParametersHandler _handler;

        internal Setup(Func<GatewayContext, ValidateParametersConfig, bool> predicate,
            ValidateParametersHandler handler)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateParametersConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }
    }
}