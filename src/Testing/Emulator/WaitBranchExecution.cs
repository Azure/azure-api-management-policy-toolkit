// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal sealed class WaitBranchExecution : IDisposable
{
    private static readonly AsyncLocal<WaitBranchExecution?> s_current = new();
    private readonly WaitBranchExecution? _previous;
    private readonly GatewayContext _context;
    private readonly CancellationToken _cancellationToken;
    private readonly HttpTransportState _transport;
    private readonly Guid _requestId;
    private readonly WaitMessageSynchronization _messages;
    private readonly List<WaitHeaderMutation> _requestHeaders = [];
    private readonly List<WaitHeaderMutation> _responseHeaders = [];
    private WaitMessageSnapshot _snapshot;
    private int _completed;

    public WaitBranchExecution(GatewayContext context, CancellationToken cancellationToken)
    {
        _context = context;
        _cancellationToken = cancellationToken;
        _requestId = context.RequestId;
        _messages = context.WaitMessages
            ?? throw new InvalidOperationException("Wait branch requires shared message synchronization.");
        _snapshot = _messages.Capture(context);
        _transport = context.Services.Resolve<HttpTransportState>()
            ?? throw new InvalidOperationException("Wait branch requires its own transport cancellation state.");
        _previous = s_current.Value;
        s_current.Value = this;
    }

    internal static bool IsExecuting(GatewayContext context) =>
        s_current.Value is { } execution && ReferenceEquals(execution._context, context);

    internal static void ValidateAccess(GatewayContext context)
    {
        if (s_current.Value is not { } execution)
        {
            return;
        }
        if (!ReferenceEquals(execution._context, context))
        {
            throw new InvalidOperationException(
                "A wait branch must use its supplied section proxy, not a captured outer gateway context or proxy.");
        }
        if (Volatile.Read(ref execution._completed) != 0)
        {
            throw new InvalidOperationException("The wait branch has completed; its proxy can no longer execute.");
        }
        if (context.RequestId != execution._requestId)
        {
            throw new InvalidOperationException("A Wait branch must retain its logical RequestId.");
        }
        if (!ReferenceEquals(context.Services.Resolve<HttpTransportState>(), execution._transport))
        {
            throw new NotSupportedException(
                "A Wait branch cannot replace its owned HttpTransportState or cancellation token. " +
                "Configure caller cancellation before executing Wait.");
        }
        execution._cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void Synchronize(GatewayContext context)
    {
        ValidateAccess(context);
        if (s_current.Value is { } execution)
        {
            execution._snapshot = execution._messages.Synchronize(context, execution._snapshot, execution._cancellationToken,
                execution._requestHeaders, execution._responseHeaders);
            execution._requestHeaders.Clear();
            execution._responseHeaders.Clear();
        }
    }

    internal static void Publish(GatewayContext context)
    {
        if (s_current.Value is { } execution && ReferenceEquals(execution._context, context))
        {
            execution._snapshot = execution._messages.Publish(context, execution._snapshot, execution._cancellationToken,
                execution._requestHeaders, execution._responseHeaders);
            execution._requestHeaders.Clear();
            execution._responseHeaders.Clear();
        }
    }

    internal static void RecordHeaderMutation(
        GatewayContext context, Dictionary<string, string[]> headers, string name, bool removeCaseVariants = false)
    {
        if (s_current.Value is not { } execution || !ReferenceEquals(execution._context, context))
        {
            return;
        }
        var mutations = ReferenceEquals(headers, context.Request.Headers) ? execution._requestHeaders
            : ReferenceEquals(headers, context.Response.Headers) ? execution._responseHeaders
            : throw new InvalidOperationException("A Wait header mutation must belong to its branch message.");
        mutations.Add(new WaitHeaderMutation(name, removeCaseVariants));
    }

    public void Dispose()
    {
        try
        {
            Publish(_context);
        }
        finally
        {
            Interlocked.Exchange(ref _completed, 1);
            s_current.Value = _previous;
        }
    }
}

internal readonly record struct WaitHeaderMutation(string Name, bool RemoveCaseVariants);