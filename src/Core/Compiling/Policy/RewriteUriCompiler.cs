// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class RewriteUriCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.RewriteUri);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count is > 2 or 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "rewrite-uri"));
            return;
        }

        var element = new XElement("rewrite-uri");
        var template = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        element.Add(new XAttribute("template", template));

        if (node.ArgumentList.Arguments.Count == 2)
        {
            var copyUnmatchedParams = node.ArgumentList.Arguments[1].Expression.ProcessParameter(context);
            element.Add(new XAttribute("copy-unmatched-params", copyUnmatchedParams));
        }

        context.AddPolicy(element);
    }
}