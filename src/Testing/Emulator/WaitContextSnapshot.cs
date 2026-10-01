// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal sealed class WaitContextSnapshot
{
    private readonly GatewayContext _parent;
    private readonly Dictionary<string, object> _variables;
    private readonly Dictionary<string, IPolicyHandler> _handlers;

    internal string SectionName { get; }
    internal WaitMessageSynchronization Messages { get; }

    internal WaitContextSnapshot(GatewayContext context)
    {
        _parent = context;
        SectionName = context.CurrentSectionName
            ?? throw new InvalidOperationException("Wait must execute within a section proxy.");
        if (SectionName is not (nameof(IInboundContext) or nameof(IBackendContext)
            or nameof(IOutboundContext) or nameof(IOnErrorContext)))
        {
            throw new NotSupportedException($"Wait cannot execute in runtime section '{SectionName}'.");
        }
        _handlers = WaitHandlerSnapshot.Copy(context.CurrentSectionHandlers
            ?? throw new InvalidOperationException("Wait requires the calling section's policy handlers."));
        ArgumentNullException.ThrowIfNull(context.Variables);
        _variables = new(context.Variables, context.Variables.Comparer);
        Messages = context.WaitMessages ?? new WaitMessageSynchronization(context);
        context.Services.Register(context.Services.Resolve<RateLimitStore>() ?? context.RateLimitStore);
        if (context.Services.Resolve<IConcurrencyLimiter>() is null)
        {
            context.Services.Register<IConcurrencyLimiter>(new KeyedConcurrencyLimiter());
        }
    }

    internal GatewayContext CreateBranch(CancellationToken cancellationToken)
    {
        var target = new GatewayContext(_parent)
        {
            RequestId = _parent.RequestId,
            Timestamp = _parent.Timestamp,
            Elapsed = _parent.Elapsed,
            Tracing = _parent.Tracing,
            Api = WaitMessageSynchronization.CopyModel(_parent.Api),
            Subscription = WaitMessageSynchronization.CopyModel(_parent.Subscription),
            User = WaitMessageSynchronization.CopyModel(_parent.User),
            Deployment = WaitMessageSynchronization.CopyModel(_parent.Deployment),
            Operation = WaitMessageSynchronization.CopyModel(_parent.Operation),
            Product = WaitMessageSynchronization.CopyModel(_parent.Product),
            LastError = CopyError(_parent.LastError),
            Variables = CopyVariables(),
            Trace = Messages.Trace,
            BackendUrl = _parent.BackendUrl,
            ManagedIdentityTokenProvider = _parent.ManagedIdentityTokenProvider,
            CurrentSectionName = SectionName,
            WaitMessages = Messages
        };
        target.Services.Register(HttpPolicyTransport.GetState(_parent).ForkForWait(cancellationToken));
        _parent.Services.CopyForWait(target);
        _parent.CopyNamedValuesTo(target);
        foreach (var fragment in _parent.FragmentRegistry)
        {
            target.FragmentRegistry.Add(fragment.Key, fragment.Value);
        }
        target.ActiveFragments.UnionWith(_parent.ActiveFragments);
        Messages.Refresh(target);
        SemanticCacheLookupState.ForkForWait(_parent, target);
        target.InboundProxy.CopyHandlersFrom(_parent.InboundProxy);
        target.BackendProxy.CopyHandlersFrom(_parent.BackendProxy);
        target.OutboundProxy.CopyHandlersFrom(_parent.OutboundProxy);
        target.OnErrorProxy.CopyHandlersFrom(_parent.OnErrorProxy);
        return target;
    }

    private Dictionary<string, object> CopyVariables()
    {
        Dictionary<string, object> target = _parent.Variables is ApimVariablesDictionary
            ? new ApimVariablesDictionary() : new Dictionary<string, object>(_variables.Comparer);
        foreach (var variable in _variables)
        {
            target.Add(variable.Key, variable.Value);
        }
        return target;
    }

    internal Dictionary<string, IPolicyHandler> CreateHandlers() => WaitHandlerSnapshot.Copy(_handlers);

    internal void MergeChangedVariables(GatewayContext parent, IEnumerable<WaitBranchResult> children)
    {
        foreach (var child in children)
        {
            foreach (var variable in child.Variables)
            {
                if (!_variables.TryGetValue(variable.Key, out var original) || !Equals(original, variable.Value))
                {
                    parent.Variables[variable.Key] = variable.Value;
                }
            }
        }
    }

    internal static void PropagateResult(GatewayContext parent, WaitBranchResult child)
    {
        foreach (var variable in child.Variables)
        {
            parent.Variables[variable.Key] = variable.Value;
        }
        if (child.Error is not null)
        {
            parent.LastError = CopyError(child.Context.LastError);
            child.Error.Throw();
        }
    }

    internal static MockLastError CopyError(MockLastError error) => new()
    {
        Source = error.Source,
        Reason = error.Reason,
        Message = error.Message,
        Scope = error.Scope,
        Section = error.Section,
        Path = error.Path,
        PolicyId = error.PolicyId,
        HttpErrorCode = error.HttpErrorCode
    };
}

internal sealed record WaitBranchResult(
    GatewayContext Context,
    Dictionary<string, object> Variables,
    System.Runtime.ExceptionServices.ExceptionDispatchInfo? Error);