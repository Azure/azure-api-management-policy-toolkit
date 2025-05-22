// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface IMethodPolicyHandler
{
    string MethodName { get; }
    void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node);
}