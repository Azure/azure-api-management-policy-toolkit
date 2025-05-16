// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public interface IReturnValueMethodPolicyHandler
{
    string MethodName { get; }
    void Handle(ICompilationContext context, InvocationExpressionSyntax node, string variableName);
}