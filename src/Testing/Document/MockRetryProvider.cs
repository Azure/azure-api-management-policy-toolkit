// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>
/// Configures retry-policy callbacks. Default execution retries only after successful
/// child execution and a true condition; unhandled errors and termination propagate.
/// Delays use a registered IRetryScheduler service, or a deterministic virtual scheduler.
/// </summary>
public static class MockRetryProvider
{
    public static Setup Retry<T>(this MockPoliciesProvider<T> mock) where T : class =>
        Retry(mock, (_, _, _) => true);

    public static Setup Retry<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, RetryConfig, Action, bool> predicate
    ) where T : class
    {
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentNullException.ThrowIfNull(predicate);
        var handler = mock.SectionContextProxy.GetHandler<RetryHandler>();
        return new Setup(predicate, handler);
    }

    public class Setup
    {
        private readonly Func<GatewayContext, RetryConfig, Action, bool> _predicate;
        private readonly RetryHandler _handler;

        internal Setup(
            Func<GatewayContext, RetryConfig, Action, bool> predicate,
            RetryHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        public void WithCallback(Action<GatewayContext, RetryConfig, Action> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
        }
    }
}
