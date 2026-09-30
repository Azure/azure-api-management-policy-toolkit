// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>
/// Configures explicit wait-policy mocks. Default parallel execution requires child
/// capture, isolated gateway state, and cancellation support from the execution proxy.
/// </summary>
public static class MockWaitProvider
{
    /// <summary>
    /// Configures a callback for any valid wait-policy invocation.
    /// </summary>
    public static Setup Wait<T>(this MockPoliciesProvider<T> mock) where T : class =>
        Wait(mock, (_, _, _) => true);

    /// <summary>
    /// Configures a callback for wait-policy invocations matching the predicate.
    /// </summary>
    public static Setup Wait<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, Action, string?, bool> predicate) where T : class
    {
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentNullException.ThrowIfNull(predicate);
        var handler = mock.SectionContextProxy.GetHandler<WaitHandler>();
        return new Setup(predicate, handler);
    }

    /// <summary>
    /// Configures an explicit override, not a simulation of parallel child execution.
    /// </summary>
    public sealed class Setup
    {
        private readonly Func<GatewayContext, Action, string?, bool> _predicate;
        private readonly WaitHandler _handler;

        internal Setup(Func<GatewayContext, Action, string?, bool> predicate, WaitHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        /// <summary>
        /// Overrides wait execution. The callback receives the original child delegate
        /// and authored wait-for value; invoking the delegate remains synchronous.
        /// </summary>
        public void WithCallback(Action<GatewayContext, Action, string?> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackHooks.Add((_predicate, callback).ToTuple());
        }
    }
}
