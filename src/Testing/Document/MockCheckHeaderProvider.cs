// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Document;

public static class MockCheckHeaderProvider
{
    public static Setup CheckHeader(this MockPoliciesProvider<IInboundContext> mock) =>
        CheckHeader(mock, (_, _) => true);

    public static Setup CheckHeader(
        this MockPoliciesProvider<IInboundContext> mock,
        Func<GatewayContext, CheckHeaderConfig, bool> predicate
    )
    {
        var handler = mock.SectionContextProxy.GetHandler<CheckHeaderHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, CheckHeaderConfig, bool> _predicate;
        private readonly CheckHeaderHandler _handler;

        internal Setup(
            Func<GatewayContext, CheckHeaderConfig, bool> predicate,
            CheckHeaderHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, CheckHeaderConfig> callback) =>
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());

        public void OnCheckPass(Action<GatewayContext, CheckHeaderConfig> onCheckPass) =>
            _handler.OnCheckPassed.Add((_predicate, onCheckPass).ToTuple());

        public void OnCheckFail(Action<GatewayContext, CheckHeaderConfig> onCheckFail) =>
            _handler.OnCheckFailed.Add((_predicate, onCheckFail).ToTuple());
    }
}