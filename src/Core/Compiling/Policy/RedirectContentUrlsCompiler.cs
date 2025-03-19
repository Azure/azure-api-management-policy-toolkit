// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class RedirectContentUrlsCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.RedirectContentUrls);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        context.AddPolicy(new XElement("redirect-content-urls"));
    }
}