// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class PolicyServiceAwaiter
{
    internal static T Wait<T>(GatewayContext context, Task<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        HttpPolicyTransport.ObserveFault(operation);
        var cancellationToken = HttpPolicyTransport.GetCancellationToken(context);
        var result = operation.WaitAsync(cancellationToken).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static void Wait(GatewayContext context, Task operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        HttpPolicyTransport.ObserveFault(operation);
        var cancellationToken = HttpPolicyTransport.GetCancellationToken(context);
        operation.WaitAsync(cancellationToken).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
    }
}