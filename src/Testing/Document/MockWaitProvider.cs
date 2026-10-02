// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>
/// Configures explicit wait-policy mocks. Legacy Action overloads require a mock;
/// typed branch overloads execute in parallel by default.
/// </summary>
/// <remarks>
/// Typed branches must use their supplied section proxies. Conditional choose delegates may execute
/// every handler registered for the calling section, retaining each handler's normal limitations.
/// Handler setups and context-owned service instances are branch-local; caches, telemetry, concurrency
/// limits, and logical-request accounting are shared. Message deltas are published atomically at policy
/// boundaries rather than sharing the public mocks' non-thread-safe dictionaries during blocked operations.
/// Variable dictionaries are copied shallowly, as in the gateway. All merges changed entries in authored
/// order without propagating removals. Any propagates the winner's entries, including before its error,
/// cancels other branches, and does not undo their already-executed external or message side effects.
/// A terminating policy ends its child pipeline; its response effects are shared but its stage is not
/// copied to the parent. Wait nested within Retry remains invalid.
/// Scalar body/status writes and message replacements are recorded even when they match a previous
/// working snapshot. Callback reads observe a policy-boundary snapshot, not a physically shared object.
/// Mutable variable values deliberately remain shared references; callers must synchronize their mutation.
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
    /// This setup matches the pipeline section's delegate type. Use WaitFragmentBranches() for typed
    /// Wait invocations inside fragments included from this section.
    /// </summary>
    public static BranchSetup<T> WaitBranches<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, Action<T>[], string?, bool>? predicate = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(mock);
        return new BranchSetup<T>(predicate ?? ((_, _, _) => true),
            mock.SectionContextProxy.GetHandler<WaitHandler>());
    }

    /// <summary>
    /// Configures an override for typed Wait invocations inside fragments included from this setup section.
    /// The callback receives the original Action&lt;IFragmentContext&gt; array through the calling section's
    /// Wait handler. It does not match pipeline-typed Wait invocations or implicitly execute its branches.
    /// </summary>
    public static BranchSetup<IFragmentContext> WaitFragmentBranches<T>(
        this MockPoliciesProvider<T> mock,
        Func<GatewayContext, Action<IFragmentContext>[], string?, bool>? predicate = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(mock);
        return new BranchSetup<IFragmentContext>(predicate ?? ((_, _, _) => true),
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