// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateJwtProvider
{
    public static Setup ValidateJwt(this MockPoliciesProvider<IInboundContext> mock) =>
        ValidateJwt(mock, (_, _) => true);

    public static Setup ValidateJwt(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ValidateJwtConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<ValidateJwtHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, ValidateJwtConfig, bool> _predicate;
        private readonly ValidateJwtHandler _handler;

        internal Setup(
            Func<GatewayContext, ValidateJwtConfig, bool> predicate,
            ValidateJwtHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateJwtConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
    }
}