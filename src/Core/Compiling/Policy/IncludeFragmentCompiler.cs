// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class IncludeFragmentCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.IncludeFragment);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "include-fragment"
            ));
            return;
        }

        var fragmentId = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        context.AddPolicy(new XElement("include-fragment", new XAttribute("fragment-id", fragmentId)));
    }
}