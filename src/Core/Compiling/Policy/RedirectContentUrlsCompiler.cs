// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class RedirectContentUrlsCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.RedirectContentUrls);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        context.AddPolicy(new XElement("redirect-content-urls"));
    }
}