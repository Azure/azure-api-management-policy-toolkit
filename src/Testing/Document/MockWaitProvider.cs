// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>
/// Configures explicit wait-policy mocks. Legacy Action overloads require a mock;
/// typed branch overloads execute in parallel by default.
/// </summary>
/// <remarks>
/// Typed branches must use their supplied section proxies. Conditional choose delegates may perform
/// SendRequest and CacheLookupValue operations; other policies and branch pipeline termination are rejected.
/// All merges only changed variable outputs and rejects conflicting writes. Any observes the first
/// completed delegate, including errors, cancels the others, and commits only its isolated output snapshot.
/// Unsupported mutable inputs and caller-owned gateway certificates require an explicit mock.
/// Injected services, token providers, child callbacks, and trace sinks must support concurrent calls;
/// callbacks must not mutate a captured outer gateway or replace a branch's transport cancellation state.
/// Cancellation prevents further proxy execution but cannot forcibly stop arbitrary synchronous callback code.
/// </remarks>
public static class MockWaitProvider
{
    /// <summary>
    /// Configures a callback for any valid legacy wait-policy invocation.
    /// </summary>
    public static Setup Wait<T>(this MockPoliciesProvider<T> mock) where T : class =>
        Wait(mock, (_, _, _) => true);

    /// <summary>
    /// Configures a callback for legacy wait-policy invocations matching the predicate.
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
    /// Configures an explicit override of a typed Wait invocation. Branch delegates are passed unchanged;
    /// this mock does not simulate parallel execution. Existing Wait() mocks apply only to the legacy overload.
    /// </summary>
    public static BranchSetup<T> WaitBranches<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, Action<T>[], string?, bool>? predicate = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(mock);
        return new BranchSetup<T>(predicate ?? ((_, _, _) => true),
            mock.SectionContextProxy.GetHandler<WaitHandler>());
    }

    /// <summary>Configures typed branch callbacks for the setup section.</summary>
    public sealed class BranchSetup<T> where T : class
    {
        private readonly Func<GatewayContext, Action<T>[], string?, bool> _predicate;
        private readonly WaitHandler _handler;

        internal BranchSetup(Func<GatewayContext, Action<T>[], string?, bool> predicate, WaitHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        /// <summary>Overrides typed Wait execution without implicitly executing its branches.</summary>
        public void WithCallback(Action<GatewayContext, Action<T>[], string?> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.BranchCallbackHooks.Add((typeof(T),
                (gateway, branches, mode) => _predicate(gateway, (Action<T>[])branches, mode),
                (gateway, branches, mode) => callback(gateway, (Action<T>[])branches, mode)));
        }
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