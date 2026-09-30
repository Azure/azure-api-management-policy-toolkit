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
    private static readonly ConditionalWeakTable<GatewayContext, SemanticCacheLookupState> s_pending = new();

    internal static void Clear(GatewayContext context) => s_pending.Remove(context);

    internal void Remember(GatewayContext context)
    {
        Clear(context);
        s_pending.Add(context, this);
    }

    internal static SemanticCacheLookupState GetRequired(GatewayContext context)
    {
        if (!s_pending.TryGetValue(context, out var state))
        {
            throw new InvalidOperationException(
                "Semantic cache store requires a corresponding inbound semantic cache lookup miss for this request.");
        }

        if (state.RequestId != context.RequestId || !ReferenceEquals(state.Request, context.Request))
        {
            throw new InvalidOperationException("Semantic cache lookup state belongs to a different request.");
        }

        return state;
    }
}