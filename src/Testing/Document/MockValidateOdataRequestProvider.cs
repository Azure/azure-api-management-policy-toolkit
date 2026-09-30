// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateOdataRequestProvider
{
    public static Setup ValidateOdataRequest(this MockPoliciesProvider<IInboundContext> mock) =>
        mock.ValidateOdataRequest((_, _) => true);

    public static Setup ValidateOdataRequest(this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ValidateOdataRequestConfig, bool> predicate) =>
        new(predicate, mock.SectionContextProxy.GetHandler<ValidateOdataRequestHandler>());

    public sealed class Setup
    {
        private readonly Func<GatewayContext, ValidateOdataRequestConfig, bool> _predicate;
        private readonly ValidateOdataRequestHandler _handler;

        internal Setup(Func<GatewayContext, ValidateOdataRequestConfig, bool> predicate,
            ValidateOdataRequestHandler handler)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateOdataRequestConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }
    }
}