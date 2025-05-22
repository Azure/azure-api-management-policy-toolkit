// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.Contracts;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Assertions;

public static class AssertionExtensions
{
    [Pure]
    public static CompilationResultAssertion Should(this ICompilationResult compilationResult)
    {
        return new CompilationResultAssertion(compilationResult);
    }
}