// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Basic.Reference.Assemblies;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers.Test;

public enum AnalyzerTestTargetFramework
{
    Net80,
    Net100
}

public class BaseAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, MSTestVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public BaseAnalyzerTest(string source, params DiagnosticResult[] diags)
        : this(AnalyzerTestTargetFramework.Net80, source, diags)
    {
    }

    BaseAnalyzerTest(
        AnalyzerTestTargetFramework targetFramework,
        string source,
        params DiagnosticResult[] diags)
    {
        var (targetFrameworkMoniker, references) = targetFramework switch
        {
            AnalyzerTestTargetFramework.Net80 => ("net8.0", Net80.References.All),
            _ => throw new ArgumentOutOfRangeException(nameof(targetFramework), targetFramework, null)
        };

        ReferenceAssemblies = new ReferenceAssemblies(targetFrameworkMoniker);
        TestState.AdditionalReferences.AddRange(references);
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