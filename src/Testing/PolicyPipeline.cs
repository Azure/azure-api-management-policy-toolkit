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
    /// <exception cref="ArgumentException">The fragment ID is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The fragment ID or instance is null.</exception>
    public PolicyPipeline RegisterFragment(string fragmentId, IFragment fragment)
    {
        Context.RegisterFragment(fragmentId, fragment);
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
    /// affect each other. Isolated failures are reported through the context's Trace callback.
    /// </summary>
    public void RunInboundIndependent()
    {
        foreach (var scope in InboundOrder)
        {
            if (_policies.TryGetValue(scope, out var doc))
            {
                HandleIsolated(scope, Context.InboundProxy.Object, doc.Inbound);
            }
        }
    }

    /// <summary>Runs backend sections from Global → Operation.</summary>
    public void RunBackend()
    {
        if (Context.ResponseTerminated || _policies.Count == 0)
        {
            return;
        }

        Context.ExecuteBackend(() =>
        {
            foreach (var scope in InboundOrder)
            {
                if (Context.ResponseTerminated) return;
                if (_policies.TryGetValue(scope, out var doc))
                {
                    Handle(Context.BackendProxy.Object, doc.Backend);
                }
            }
        });
    }

    /// <summary>Runs backend sections independently.</summary>
    public void RunBackendIndependent()
    {
        if (_policies.Count == 0)
        {
            return;
        }

        Context.ExecuteBackend(() =>
        {
            foreach (var scope in InboundOrder)
            {
                if (_policies.TryGetValue(scope, out var doc))
                {
                    Handle(Context.BackendProxy.Object, doc.Backend);
                }
            }
        });
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
            if (Context.ResponseTerminated) return;
            if (_policies.TryGetValue(scope, out var doc))
            {
                Handle(Context.OnErrorProxy.Object, doc.OnError);
            }
        }
    }

    /// <summary>
    /// Runs inbound, backend, and outbound sections in order and settles deferred
    /// limiter work after the final response. Execution errors propagate without settlement.
    /// </summary>
    public void RunAll() => RunRequest(request =>
    {
        request.RunInbound();
        if (!Context.ResponseTerminated) request.RunBackend();
        if (!Context.ResponseTerminated) request.RunOutbound();
    });

    /// <summary>
    /// Runs inbound, backend, and outbound using nested Base() chaining and settles
    /// deferred limiter work after all scope frames and hooks have been restored.
    /// </summary>
    public void RunAllNested() => RunRequest(request =>
    {
        request.RunInboundNested();
        if (!Context.ResponseTerminated) request.RunBackendNested();
        if (!Context.ResponseTerminated) request.RunOutboundNested();
    });

    /// <summary>
    /// Executes a synchronous logical request. Include any flat or nested on-error
    /// processing, independent sections, and final response postprocessing in the
    /// callback. Nested boundaries settle only after the successful outermost return.
    /// Individual section methods do not perform automatic settlement.
    /// </summary>
    /// <exception cref="ArgumentNullException">The request callback is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// RequestId changes during execution, completed limiter work is extended,
    /// or the same context is used concurrently.
    /// </exception>
    public void RunRequest(Action<PolicyPipeline> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Context.ExecuteRequest(() => request(this));
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
        if (scopes.Count == 0 || Context.ResponseTerminated) return;
        Context.ExecuteBackend(() => RunNestedForSection(scopes, Context.BackendProxy, (doc, ctx) => doc.Backend(ctx)));
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
        if (Context.ResponseTerminated)
        {
            return;
        }

        Context.ExecuteSection(() => RunNestedForSectionCore(scopes, proxy, runSection));
    }

    private void RunNestedForSectionCore<T>(
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
            catch (FinishSectionProcessingException termination)
            {
                Context.RecordTermination(termination);
            }
            finally
            {
                baseHandler.CallbackHooks.Clear();
                baseHandler.CallbackHooks.AddRange(savedHooks);
            }
        }

        RunNested(0);
    }

    private void Handle<T>(T context, Action<T> section)
        => Context.ExecuteSection(() => HandleSection(context, section));

    private void HandleSection<T>(T context, Action<T> section)
    {
        try
        {
            section(context);
        }
        catch (FinishSectionProcessingException termination)
        {
            Context.RecordTermination(termination);
        }
    }

    /// <summary>
    /// Handles an inbound section with legacy exception isolation. Failures are traced
    /// so that subsequent scopes can still run without silently discarding errors.
    /// </summary>
    private void HandleIsolated<T>(PolicyScope scope, T context, Action<T> section) => Context.ExecuteSection(() =>
    {
        try
        {
            HandleSection(context, section);
        }
        catch (Exception error)
        {
            Context.Trace($"Independent inbound execution failed at scope '{scope}': {error}");
        }
    });
}