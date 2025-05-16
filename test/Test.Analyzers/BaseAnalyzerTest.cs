// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

public class BaseAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, MSTestVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public BaseAnalyzerTest(string source, params DiagnosticResult[] diags)
    {
        ReferenceAssemblies = new ReferenceAssemblies("net8.0",
            new PackageIdentity("Microsoft.NETCore.App.Ref", "8.0.0"), Path.Combine("ref", "net8.0"));
        TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(ExpressionAttribute).Assembly.Location));
        TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Expression<>).Assembly.Location));
        TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(IExpressionContext).Assembly.Location));
        TestState.Sources.Add(
            $"""
             using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
             using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

             namespace Mielek.Test;

             {source}
             """
        );
        TestState.ExpectedDiagnostics.AddRange(diags);
    }
}