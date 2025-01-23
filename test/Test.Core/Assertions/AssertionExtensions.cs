// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.Contracts;

using Azure.ApiManagement.PolicyToolkit.Compiling;

namespace Azure.ApiManagement.PolicyToolkit.Assertions;

public static class AssertionExtensions
{
    [Pure]
    public static CompilationResultAssertion Should(this ICompilationResult compilationResult)
    {
        return new CompilationResultAssertion(compilationResult);
    }
}