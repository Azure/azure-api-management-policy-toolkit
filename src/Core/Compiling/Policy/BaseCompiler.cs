// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class BaseCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.Base);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax _)
    {
        context.AddPolicy(new XElement("base"));
    }
}