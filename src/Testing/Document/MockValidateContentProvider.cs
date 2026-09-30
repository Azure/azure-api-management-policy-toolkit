// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockValidateContentProvider
{
    public static Setup ValidateContent(this MockPoliciesProvider<IInboundContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateContent(this MockPoliciesProvider<IOutboundContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateContent(this MockPoliciesProvider<IOnErrorContext> mock) =>
        Create(mock, (_, _) => true);

    public static Setup ValidateContent(this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, ValidateContentConfig, bool> predicate) => Create(mock, predicate);

    public static Setup ValidateContent(this MockPoliciesProvider<IOutboundContext> mock,
        Func<GatewayContext, ValidateContentConfig, bool> predicate) => Create(mock, predicate);

    public static Setup ValidateContent(this MockPoliciesProvider<IOnErrorContext> mock,
        Func<GatewayContext, ValidateContentConfig, bool> predicate) => Create(mock, predicate);

    private static Setup Create<T>(MockPoliciesProvider<T> mock,
        Func<GatewayContext, ValidateContentConfig, bool> predicate) where T : class =>
        new(predicate, mock.SectionContextProxy.GetHandler<ValidateContentHandler>());

    public sealed class Setup
    {
        private readonly Func<GatewayContext, ValidateContentConfig, bool> _predicate;
        private readonly ValidateContentHandler _handler;

        internal Setup(Func<GatewayContext, ValidateContentConfig, bool> predicate, ValidateContentHandler handler)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, ValidateContentConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }
    }
}