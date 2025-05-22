// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

public static partial class Rules
{
    public static class TypeUsed
    {
        public readonly static DiagnosticDescriptor DisallowedType = new DiagnosticDescriptor(
            "APIM001",
            "Disallowed type used",
            "Type '{0}' is not allowed",
            "Type",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Description.",
            helpLinkUri: "TODO",
            customTags: new[] { "APIM", "ApiManagement" });

        public readonly static DiagnosticDescriptor DisallowedMember = new DiagnosticDescriptor(
            "APIM002",
            "Disallowed member used",
            "Member '{0}' is not allowed",
            "Type",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Description.",
            helpLinkUri: "TODO",
            customTags: new[] { "APIM", "ApiManagement" });


        public readonly static ImmutableArray<DiagnosticDescriptor> All = ImmutableArray.Create(
            DisallowedType, DisallowedMember
        );
    }
}