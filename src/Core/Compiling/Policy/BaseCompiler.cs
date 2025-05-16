// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class BaseCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.Base);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax _)
    {
        context.AddPolicy(new XElement("base"));
    }
}