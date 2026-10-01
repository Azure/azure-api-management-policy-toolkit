// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

internal sealed record SemanticCacheLookupState(
    Guid RequestId,
    MockRequest Request,
    ICache Cache,
    TimeProvider Clock,
    string PartitionKey,
    string PromptKey,
    IReadOnlyList<double>? Embedding)
{
    private static readonly ConditionalWeakTable<GatewayContext, PendingState> s_pending = new();

    internal static void Clear(GatewayContext context)
    {
        if (s_pending.TryGetValue(context, out var pending))
        {
            lock (pending)
            {
                pending.Value = null;
            }
        }
    }

    internal void Remember(GatewayContext context)
    {
        var pending = s_pending.GetValue(context, _ => new PendingState());
        lock (pending)
        {
            pending.Value = this with { Request = context.WaitMessages?.RequestIdentity ?? context.Request };
        }
    }

    internal static void ForkForWait(GatewayContext source, GatewayContext target)
    {
        var pending = s_pending.GetValue(source, _ => new PendingState());
        s_pending.Add(target, pending);
    }

    internal static SemanticCacheLookupState GetRequired(GatewayContext context)
    {
        if (!s_pending.TryGetValue(context, out var pending))
        {
            throw new InvalidOperationException(
                "Semantic cache store requires a corresponding inbound semantic cache lookup miss for this request.");
        }

        lock (pending)
        {
            var state = pending.Value ?? throw new InvalidOperationException(
                "Semantic cache store requires a corresponding inbound semantic cache lookup miss for this request.");
            if (state.RequestId != context.RequestId
                || !ReferenceEquals(state.Request, context.WaitMessages?.RequestIdentity ?? context.Request))
            {
                throw new InvalidOperationException("Semantic cache lookup state belongs to a different request.");
            }
            return state;
        }
    }

    private sealed class PendingState
    {
        internal SemanticCacheLookupState? Value;
    }
}