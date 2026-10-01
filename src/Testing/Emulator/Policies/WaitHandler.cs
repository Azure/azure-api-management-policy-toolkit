// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class WaitHandler : IPolicyHandler
{
    public List<Tuple<
        Func<GatewayContext, Action, string?, bool>,
        Action<GatewayContext, Action, string?>
    >> CallbackHooks
    { get; } = [];

    internal List<(
        Type SectionType,
        Func<GatewayContext, Array, string?, bool> Predicate,
        Action<GatewayContext, Array, string?> Callback
    )> BranchCallbackHooks
    { get; } = [];

    public string PolicyName => nameof(IInboundContext.Wait);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        if (args is not { Length: 2 })
        {
            throw new ArgumentException("Expected 2 arguments.", nameof(args));
        }
        if (context.CurrentPolicyMethod?.GetParameters() is [var mode, var branches]
            && mode.ParameterType == typeof(string))
        {
            if (args[0] is not (null or string))
            {
                throw new ArgumentException("Wait for must be 'all' or 'any'.", "waitFor");
            }
            var typedWaitFor = (string?)args[0];
            ValidateMode(typedWaitFor);
            if (branches.ParameterType == typeof(Action<IInboundContext>[]))
            {
                HandleTyped<IInboundContext>(context, args[1], typedWaitFor);
            }
            else if (branches.ParameterType == typeof(Action<IBackendContext>[]))
            {
                HandleTyped<IBackendContext>(context, args[1], typedWaitFor);
            }
            else if (branches.ParameterType == typeof(Action<IOutboundContext>[]))
            {
                HandleTyped<IOutboundContext>(context, args[1], typedWaitFor);
            }
            else if (branches.ParameterType == typeof(Action<IOnErrorContext>[]))
            {
                HandleTyped<IOnErrorContext>(context, args[1], typedWaitFor);
            }
            else if (branches.ParameterType == typeof(Action<IFragmentContext>[]))
            {
                HandleTyped<IFragmentContext>(context, args[1], typedWaitFor);
            }
            else
            {
                throw new NotSupportedException($"Unsupported typed Wait signature '{context.CurrentPolicyMethod}'.");
            }
            return null;
        }

        ArgumentNullException.ThrowIfNull(args[0], "section");
        var (section, waitFor) = args.ExtractArguments<Action, string>();
        ValidateMode(waitFor);
        ValidateRetry(context);

        var callback = CallbackHooks.Find(hook => hook.Item1(context, section, waitFor));
        if (callback is not null)
        {
            callback.Item2(context, section, waitFor);
            return null;
        }

        throw new NotSupportedException(
            "Parallel wait requires immediate child capture, gateway context isolation, and child cancellation " +
            "in SectionContextProxy. The accepted execution proxy does not provide these capabilities. " +
            "Use SetupInbound().Wait().WithCallback(...) (or the appropriate section) only for an explicit mock.");
    }

    private static void ValidateMode(string? waitFor)
    {
        if (waitFor is not (null or "all" or "any"))
        {
            throw new ArgumentException("Wait for must be 'all' or 'any'.", nameof(waitFor));
        }
    }

    private static void ValidateRetry(GatewayContext context)
    {
        if (RetryHandler.IsExecuting(context))
        {
            throw new InvalidOperationException("The wait policy cannot be nested inside retry.");
        }
    }

    private void HandleTyped<TSection>(GatewayContext context, object? argument, string? waitFor) where TSection : class
    {
        ArgumentNullException.ThrowIfNull(argument, "branches");
        if (argument is not Action<TSection>[] branches)
        {
            throw new ArgumentException($"Expected section-typed branches for '{typeof(TSection).Name}'.", "branches");
        }
        if (branches.Length == 0)
        {
            throw new ArgumentException("Wait requires at least one immediate branch.", nameof(branches));
        }
        if (branches.Any(branch => branch is null))
        {
            throw new ArgumentNullException(nameof(branches), "Wait branches cannot contain null delegates.");
        }
        ValidateRetry(context);
        HttpPolicyTransport.GetCancellationToken(context).ThrowIfCancellationRequested();
        foreach (var hook in BranchCallbackHooks)
        {
            if (hook.SectionType == typeof(TSection) && hook.Predicate(context, branches, waitFor))
            {
                hook.Callback(context, branches, waitFor);
                return;
            }
        }
        Execute(context, branches, waitFor);
    }

    private static void Execute<TSection>(
        GatewayContext context, Action<TSection>[] branches, string? waitFor) where TSection : class
    {
        var callerCancellation = HttpPolicyTransport.GetCancellationToken(context);
        callerCancellation.ThrowIfCancellationRequested();
        var snapshot = new WaitContextSnapshot(context);
        var launch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCompletion = new TaskCompletionSource<BranchExecution<TSection>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = new List<BranchExecution<TSection>>(branches.Length);
        var sources = new List<CancellationTokenSource>(branches.Length);
        var tasks = new List<Task<WaitVariableChange[]>>(branches.Length);
        var cancellationTasks = new List<Task>();
        Task<WaitVariableChange[]>? selected = null;
        try
        {
            foreach (var branch in branches)
            {
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation);
                sources.Add(cancellation);
                var branchContext = snapshot.CreateBranch(cancellation.Token);
                var proxy = SectionContextProxy<TSection>.CreateWithHandlers<TSection>(
                    branchContext, snapshot.CreateHandlers(), snapshot.SectionName);
                executions.Add(new BranchExecution<TSection>(branchContext, proxy.Object, branch, cancellation));
            }
            foreach (var execution in executions)
            {
                // Synchronous authored delegates need separate owning threads; none execute against the parent context.
                var task = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        launch.Task.GetAwaiter().GetResult();
                        using var scope = new WaitBranchExecution(execution.Context, execution.Cancellation.Token);
                        WaitVariableChange[]? changes = null;
                        execution.Context.ExecuteSection(() =>
                        {
                            try
                            {
                                execution.Branch(execution.Proxy);
                            }
                            finally
                            {
                                firstCompletion.TrySetResult(execution);
                            }
                            WaitBranchExecution.ValidateAccess(execution.Context);
                            changes = snapshot.CaptureChanges(execution.Context);
                            WaitBranchExecution.ValidateAccess(execution.Context);
                        });
                        return changes ?? throw new InvalidOperationException("Wait branch did not capture its outputs.");
                    }
                    catch (FinishSectionProcessingException termination)
                    {
                        throw new NotSupportedException(
                            "A parallel Wait branch cannot terminate the enclosing pipeline. " +
                            "Use an explicit WaitBranches callback to simulate pipeline termination.", termination);
                    }
                    finally
                    {
                        firstCompletion.TrySetResult(execution);
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                execution.Task = task;
                tasks.Add(task);
            }

            // Record delegate completion before output copying so snapshot costs cannot reorder the any winner.
            launch.SetResult(true);
            WaitVariableChange[][] outputs;
            if (waitFor == "any")
            {
                var winner = firstCompletion.Task.WaitAsync(callerCancellation).GetAwaiter().GetResult();
                selected = winner.Task ?? throw new InvalidOperationException("Wait winner was not scheduled.");
                RequestCancellation(executions, selected, cancellationTasks, onlyPending: false);
                outputs = [selected.GetAwaiter().GetResult()];
            }
            else
            {
                outputs = Task.WhenAll(tasks).WaitAsync(callerCancellation).GetAwaiter().GetResult();
            }
            callerCancellation.ThrowIfCancellationRequested();
            Commit(context, outputs);
        }
        finally
        {
            try
            {
                RequestCancellation(executions, selected, cancellationTasks, onlyPending: true);
            }
            finally
            {
                launch.TrySetResult(true);
                ObserveAndDispose(tasks, sources, cancellationTasks, selected, waitFor == "any", context.Trace);
            }
        }
    }

    private static void RequestCancellation<TSection>(
        IEnumerable<BranchExecution<TSection>> executions,
        Task<WaitVariableChange[]>? selected,
        List<Task> cancellationTasks,
        bool onlyPending) where TSection : class
    {
        foreach (var execution in executions)
        {
            if (ReferenceEquals(execution.Task, selected) && selected is not null
                || onlyPending && execution.Task?.IsCompleted == true)
            {
                continue;
            }
            if (!execution.Cancellation.IsCancellationRequested)
            {
                cancellationTasks.Add(execution.Cancellation.CancelAsync());
            }
            // Mark transport tokens too, without waiting for an uncooperative service's cancellation callbacks.
            cancellationTasks.AddRange(execution.Transport.CancelPendingRequests());
        }
    }

    private static void Commit(GatewayContext context, IEnumerable<WaitVariableChange[]> outputs)
    {
        var changes = new Dictionary<string, WaitVariableChange>(context.Variables.Comparer);
        foreach (var output in outputs)
        {
            foreach (var change in output)
            {
                if (!changes.TryAdd(change.Name, change))
                {
                    throw new InvalidOperationException(
                        $"Wait branches have conflicting outputs for variable '{change.Name}'. " +
                        "Parallel branches must write distinct variables.");
                }
            }
        }
        foreach (var change in changes.Values)
        {
            if (change.Exists)
            {
                context.Variables[change.Name] = change.Value!;
            }
            else
            {
                context.Variables.Remove(change.Name);
            }
        }
    }

    private static void ObserveAndDispose(
        IReadOnlyList<Task<WaitVariableChange[]>> tasks,
        IReadOnlyList<CancellationTokenSource> sources,
        IReadOnlyList<Task> cancellations,
        Task<WaitVariableChange[]>? selected,
        bool any,
        Action<string> trace)
    {
        var detached = tasks.Where(task => !ReferenceEquals(task, selected) && (any || !task.IsCompleted)).ToArray();
        var settled = Task.WhenAll(tasks.Cast<Task>().Concat(cancellations));
        var cleanup = settled.ContinueWith(completed =>
        {
            try
            {
                _ = completed.Exception;
                foreach (var task in detached.Cast<Task>().Concat(cancellations))
                {
                    if (task.Exception is { } error && error.GetBaseException() is not OperationCanceledException)
                    {
                        trace($"Wait canceled branch or cancellation callback failed: {error.GetBaseException().Message}");
                    }
                }
            }
            finally
            {
                foreach (var source in sources)
                {
                    source.Dispose();
                }
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        HttpPolicyTransport.ObserveFault(cleanup);
    }

    private sealed class BranchExecution<TSection>(
        GatewayContext context, TSection proxy, Action<TSection> branch, CancellationTokenSource cancellation)
        where TSection : class
    {
        internal GatewayContext Context { get; } = context;
        internal TSection Proxy { get; } = proxy;
        internal Action<TSection> Branch { get; } = branch;
        internal CancellationTokenSource Cancellation { get; } = cancellation;
        internal HttpTransportState Transport { get; } = context.Services.Resolve<HttpTransportState>()
            ?? throw new InvalidOperationException("Wait branch requires its own transport cancellation state.");
        internal Task<WaitVariableChange[]>? Task { get; set; }
    }
}