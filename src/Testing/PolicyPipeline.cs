// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

/// <summary>
/// Executes policy document sections across multiple scopes in the correct order.
/// Inbound and backend sections run outer-to-inner (Global → Operation).
/// Outbound and on-error sections run inner-to-outer (Operation → Global).
/// All scopes share a single <see cref="GatewayContext"/>.
/// </summary>
public class PolicyPipeline
{
    private readonly Dictionary<PolicyScope, IDocument> _policies;

    /// <summary>
    /// The shared gateway context used by all scope documents.
    /// </summary>
    public GatewayContext Context { get; }

    /// <summary>
    /// Registers a fragment instance so that IncludeFragment calls with the given ID
    /// resolve to this instance instead of scanning assemblies via reflection.
    /// </summary>
    public PolicyPipeline RegisterFragment(string fragmentId, IFragment fragment)
    {
        Context.FragmentRegistry[fragmentId] = fragment;
        return this;
    }

    private static readonly PolicyScope[] InboundOrder =
    {
        PolicyScope.Global, PolicyScope.Workspace, PolicyScope.Product, PolicyScope.Api, PolicyScope.Operation
    };

    private static readonly PolicyScope[] OutboundOrder =
    {
        PolicyScope.Operation, PolicyScope.Api, PolicyScope.Product, PolicyScope.Workspace, PolicyScope.Global
    };

    internal PolicyPipeline(Dictionary<PolicyScope, IDocument> policies, GatewayContext context)
    {
        _policies = policies;
        Context = context;
    }

    /// <summary>Runs inbound sections from Global → Operation.</summary>
    public void RunInbound()
    {
        foreach (var scope in InboundOrder)
        {
            if (Context.ResponseTerminated) return;
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.InboundProxy.Object, doc.Inbound);
            }
        }
    }

    /// <summary>
    /// Runs inbound sections independently with scope-level isolation. Each scope runs
    /// regardless of whether a previous scope called ReturnResponse or threw an exception.
    /// Base() is a no-op. This matches legacy test harness behavior where scopes don't
    /// affect each other.
    /// </summary>
    public void RunInboundIndependent()
    {
        foreach (var scope in InboundOrder)
        {
            if (_policies.TryGetValue(scope, out var doc))
            {
                HandleIsolated(Context.InboundProxy.Object, doc.Inbound);
            }
        }
    }

    /// <summary>Runs backend sections from Global → Operation.</summary>
    public void RunBackend()
    {
        foreach (var scope in InboundOrder)
        {
            if (Context.ResponseTerminated) return;
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.BackendProxy.Object, doc.Backend);
            }
        }
    }

    /// <summary>Runs backend sections independently.</summary>
    public void RunBackendIndependent()
    {
        foreach (var scope in InboundOrder)
        {
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.BackendProxy.Object, doc.Backend);
            }
        }
    }

    /// <summary>Runs outbound sections from Operation → Global.</summary>
    public void RunOutbound()
    {
        foreach (var scope in OutboundOrder)
        {
            if (Context.ResponseTerminated) return;
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.OutboundProxy.Object, doc.Outbound);
            }
        }
    }

    /// <summary>Runs outbound sections independently.</summary>
    public void RunOutboundIndependent()
    {
        foreach (var scope in OutboundOrder)
        {
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.OutboundProxy.Object, doc.Outbound);
            }
        }
    }

    /// <summary>Runs on-error sections from Operation → Global.</summary>
    public void RunOnError()
    {
        foreach (var scope in OutboundOrder)
        {
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.OnErrorProxy.Object, doc.OnError);
            }
        }
    }

    /// <summary>Runs inbound, backend, and outbound sections in order.</summary>
    public void RunAll()
    {
        RunInbound();
        if (!Context.ResponseTerminated) RunBackend();
        if (!Context.ResponseTerminated) RunOutbound();
    }

    /// <summary>Runs inbound, backend, and outbound sections using nested Base() chaining.</summary>
    public void RunAllNested()
    {
        RunInboundNested();
        if (!Context.ResponseTerminated) RunBackendNested();
        if (!Context.ResponseTerminated) RunOutboundNested();
    }

    /// <summary>
    /// Runs inbound sections with nested Base() chaining.
    /// When a scope calls Base(), the next inner scope's inbound section executes before
    /// control returns to the calling scope's code after Base(). This matches APIM's
    /// actual execution model where scopes are nested, not flat.
    /// </summary>
    public void RunInboundNested()
    {
        var scopes = InboundOrder.Where(s => _policies.ContainsKey(s)).ToList();
        if (scopes.Count == 0) return;
        RunNestedForSection(scopes, Context.InboundProxy, (doc, ctx) => doc.Inbound(ctx));
    }

    /// <summary>Runs backend sections with nested Base() chaining.</summary>
    public void RunBackendNested()
    {
        var scopes = InboundOrder.Where(s => _policies.ContainsKey(s)).ToList();
        if (scopes.Count == 0) return;
        RunNestedForSection(scopes, Context.BackendProxy, (doc, ctx) => doc.Backend(ctx));
    }

    /// <summary>Runs outbound sections with nested Base() chaining.</summary>
    public void RunOutboundNested()
    {
        var scopes = OutboundOrder.Where(s => _policies.ContainsKey(s)).ToList();
        if (scopes.Count == 0) return;
        RunNestedForSection(scopes, Context.OutboundProxy, (doc, ctx) => doc.Outbound(ctx));
    }

    /// <summary>Runs on-error sections with nested Base() chaining.</summary>
    public void RunOnErrorNested()
    {
        var scopes = OutboundOrder.Where(s => _policies.ContainsKey(s)).ToList();
        if (scopes.Count == 0) return;
        RunNestedForSection(scopes, Context.OnErrorProxy, (doc, ctx) => doc.OnError(ctx));
    }

    private void RunNestedForSection<T>(
        List<PolicyScope> scopes,
        SectionContextProxy<T> proxy,
        Action<IDocument, T> runSection) where T : class
    {
        var baseHandler = proxy.GetHandler<BaseHandler>();

        void RunNested(int index)
        {
            if (index >= scopes.Count || Context.ResponseTerminated) return;
            var doc = _policies[scopes[index]];

            // Save and replace Base() hooks so Base() chains to next scope
            var savedHooks = new List<Tuple<Func<GatewayContext, bool>, Action<GatewayContext>>>(
                baseHandler.CallbackHooks);
            baseHandler.CallbackHooks.Clear();
            baseHandler.CallbackHooks.Add(Tuple.Create<Func<GatewayContext, bool>, Action<GatewayContext>>(
                _ => true,
                g =>
                {
                    RunNested(index + 1);
                    // Propagate return-response from inner scope
                    if (g.ResponseTerminated) throw new FinishSectionProcessingException();
                }));

            try
            {
                runSection(doc, proxy.Object);
            }
            catch (FinishSectionProcessingException) { }
            finally
            {
                baseHandler.CallbackHooks.Clear();
                baseHandler.CallbackHooks.AddRange(savedHooks);
            }
        }

        RunNested(0);
    }

    private static void Handle<T>(T context, Action<T> section)
    {
        try
        {
            section(context);
        }
        catch (FinishSectionProcessingException)
        {
        }
    }

    /// <summary>
    /// Handles a section with full exception isolation — all exceptions are caught
    /// and swallowed so that subsequent scopes can still run.
    /// </summary>
    private static void HandleIsolated<T>(T context, Action<T> section)
    {
        try
        {
            section(context);
        }
        catch
        {
        }
    }
}
