// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class LlmSemanticCacheLookupCompiler()
    : BaseSemanticCacheLookupCompiler(nameof(IInboundContext.LlmSemanticCacheLookup), "llm-semantic-cache-lookup");

public class AzureOpenAiSemanticCacheLookupCompiler()
    : BaseSemanticCacheLookupCompiler(nameof(IInboundContext.AzureOpenAiSemanticCacheLookup),
        "azure-openai-semantic-cache-lookup");

public abstract class BaseSemanticCacheLookupCompiler : IMethodPolicyHandler
{
    private readonly string _policyName;
    public string MethodName { get; }

    protected BaseSemanticCacheLookupCompiler(string methodName, string policyName)
    {
        MethodName = methodName;
        _policyName = policyName;
    }

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<SemanticCacheLookupConfig>(context, _policyName, out var values))
        {
            return;
        }

        var element = new XElement(_policyName);

        if (!element.AddAttribute(values, nameof(SemanticCacheLookupConfig.ScoreThreshold), "score-threshold"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                _policyName,
                nameof(SemanticCacheLookupConfig.ScoreThreshold)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(SemanticCacheLookupConfig.EmbeddingsBackendId),
                "embeddings-backend-id"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                _policyName,
                nameof(SemanticCacheLookupConfig.EmbeddingsBackendId)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(SemanticCacheLookupConfig.EmbeddingsBackendAuth),
                "embeddings-backend-auth"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                _policyName,
                nameof(SemanticCacheLookupConfig.EmbeddingsBackendAuth)
            ));
            return;
        }

        element.AddAttribute(values, nameof(SemanticCacheLookupConfig.IgnoreSystemMessages), "ignore-system-messages");
        element.AddAttribute(values, nameof(SemanticCacheLookupConfig.MaxMessageCount), "max-message-count");

        if (values.TryGetValue(nameof(SemanticCacheLookupConfig.VaryBy), out var varyByInitializer))
        {
            foreach (var varyBy in varyByInitializer.UnnamedValues ?? [])
            {
                element.Add(new XElement("vary-by", varyBy.Value));
            }
        }

        context.AddPolicy(element);
    }
}