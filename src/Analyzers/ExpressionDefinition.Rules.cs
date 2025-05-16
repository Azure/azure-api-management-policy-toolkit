// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

public static partial class Rules
{
    public static class Expression
    {
        public readonly static DiagnosticDescriptor ReturnTypeNotAllowed = new DiagnosticDescriptor(
            "APIM101",
            "Disallowed expression method return type",
            "Method returns '{0}' which is not an allowed expression return type",
            "Expression",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Description.",
            helpLinkUri: "TODO",
            customTags: new[] { "APIM", "ApiManagement" });

        public readonly static DiagnosticDescriptor WrongParameterCount = new DiagnosticDescriptor(
            "APIM102",
            "Too many parameters in expression method",
            "Method declares '{0}' parameters which is not allowed for an expression",
            "Expression",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Description.",
            helpLinkUri: "TODO",
            customTags: new[] { "APIM", "ApiManagement" });

        public readonly static DiagnosticDescriptor WrongParameterType = new DiagnosticDescriptor(
            "APIM103",
            "Wrong expression method parameter type",
            "Method declares '{0}' parameter type which is not allowed for an expression. Parameter should be of '{1}' type.",
            "Expression",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Description.",
            helpLinkUri: "TODO",
            customTags: new[] { "APIM", "ApiManagement" });

        public readonly static DiagnosticDescriptor WrongParameterName = new DiagnosticDescriptor(
            "APIM104",
            "Wrong expression method parameter name",
            "Method declares parameter with '{0}' name which is not allowed for an expression. Parameter should have '{1}' name.",
            "Expression",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Description.",
            helpLinkUri: "TODO",
            customTags: new[] { "APIM", "ApiManagement" });


        public readonly static ImmutableArray<DiagnosticDescriptor> All = ImmutableArray.Create(
            ReturnTypeNotAllowed, WrongParameterCount, WrongParameterName, WrongParameterType
        );
    }
}