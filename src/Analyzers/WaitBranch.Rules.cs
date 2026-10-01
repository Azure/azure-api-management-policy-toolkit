// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

public static partial class Rules
{
    public static class WaitBranch
    {
        public static readonly DiagnosticDescriptor InvalidChild = new(
            "APIM105",
            "Invalid wait branch",
            "Each wait branch must contain exactly one send-request, cache-lookup-value, or if/else-if/else policy",
            "PolicyAuthoring",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CapturedContext = new(
            "APIM106",
            "Wait branch uses the outer section context",
            "Policies inside a wait branch must use its branch context, not the outer section context",
            "PolicyAuthoring",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly ImmutableArray<DiagnosticDescriptor> All =
            [InvalidChild, CapturedContext];
    }
}
