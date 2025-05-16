// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CacheStoreCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IOutboundContext.CacheStore);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count is > 2 or < 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "cache-store"));
            return;
        }

        var element = new XElement("cache-store");

        element.Add(new XAttribute("duration", arguments[0].Expression.ProcessParameter(context)));

        if (arguments.Count == 2)
        {
            element.Add(new XAttribute("cache-response", arguments[1].Expression.ProcessParameter(context)));
        }

        context.AddPolicy(element);
    }
}