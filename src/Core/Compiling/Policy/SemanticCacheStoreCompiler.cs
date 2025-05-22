// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class LlmSemanticCacheStoreCompiler()
    : BaseSemanticCacheStoreCompiler(nameof(IOutboundContext.LlmSemanticCacheStore), "llm-semantic-cache-store");

public class AzureOpenAiSemanticCacheStoreCompiler()
    : BaseSemanticCacheStoreCompiler(nameof(IOutboundContext.AzureOpenAiSemanticCacheStore),
        "azure-openai-semantic-cache-store");

public abstract class BaseSemanticCacheStoreCompiler : IMethodPolicyHandler
{
    private readonly string _policyName;
    public string MethodName { get; }

    protected BaseSemanticCacheStoreCompiler(string methodName, string policyName)
    {
        MethodName = methodName;
        _policyName = policyName;
    }

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                _policyName));
            return;
        }

        var element = new XElement(_policyName);
        element.Add(new XAttribute("duration", arguments[0].Expression.ProcessParameter(context)));
        context.AddPolicy(element);
    }
}