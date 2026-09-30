// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

public class TestDocument(IDocument document)
{
    public GatewayContext Context { get; init; } = new();

    public void UseNamedValues(IDictionary<string, string> values)
        => Context.SetNamedValues(values);

    /// <summary>
    /// Registers a fragment instance so that IncludeFragment calls with the given ID
    /// resolve to this instance instead of scanning assemblies via reflection.
    /// </summary>
    /// <exception cref="ArgumentException">The fragment ID is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The fragment ID or instance is null.</exception>
    public TestDocument RegisterFragment(string fragmentId, IFragment fragment)
    {
        Context.RegisterFragment(fragmentId, fragment);
        return this;
    }

    public void RunInbound() => this.Handle(Context.InboundProxy.Object, document.Inbound);
    public void RunBackend() => this.Handle(Context.BackendProxy.Object, document.Backend);
    public void RunOutbound() => this.Handle(Context.OutboundProxy.Object, document.Outbound);
    public void RunOnError() => this.Handle(Context.OnErrorProxy.Object, document.OnError);

    /// <summary>
    /// Runs inbound, backend, and outbound as one coordinated request and settles
    /// deferred limiter work after the final response, including early termination.
    /// Execution errors propagate without settlement.
    /// </summary>
    public void RunAll() => RunRequest(request =>
    {
        request.RunInbound();
        if (!Context.ResponseTerminated) request.RunBackend();
        if (!Context.ResponseTerminated) request.RunOutbound();
    });

    /// <summary>
    /// Executes a synchronous logical request. Include any on-error processing or
    /// final response postprocessing in the callback. Nested boundaries share one
    /// completion owner; only a successful outer return settles limiter work.
    /// Existing individual section methods do not perform automatic settlement.
    /// </summary>
    /// <exception cref="ArgumentNullException">The request callback is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// RequestId changes during execution, completed limiter work is extended,
    /// or the same context is used concurrently.
    /// </exception>
    public void RunRequest(Action<TestDocument> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Context.ExecuteRequest(() => request(this));
    }

    private void Handle<T>(T context, Action<T> section) => Context.ExecuteSection(() =>
    {
        try
        {
            section(context);
        }
        catch (FinishSectionProcessingException termination)
        {
            Context.RecordTermination(termination);
        }
    });
}