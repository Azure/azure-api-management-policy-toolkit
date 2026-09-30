// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>
/// Explicit response-completion support for standalone emulator documents.
/// </summary>
public static class MockLimiterProvider
{
    /// <summary>
    /// Settles deferred rate increments and final quota response bandwidth.
    /// Call after outbound processing and before reusing a context for a new request.
    /// </summary>
    public static void CompleteLimiterResponse(this TestDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Context.CompleteLimiterResponse();
    }

    /// <summary>
    /// Settles limiter response state for a gateway context used by a custom request runner.
    /// </summary>
    public static void CompleteLimiterResponse(this GatewayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            context.Services.Resolve<PolicyCounterService>()?.CompleteResponse();
        }
        catch (FinishSectionProcessingException) { }
    }
}