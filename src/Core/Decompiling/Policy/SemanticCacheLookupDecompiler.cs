// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AzureOpenAiSemanticCacheLookupDecompiler()
    : BaseSemanticCacheLookupDecompiler("azure-openai-semantic-cache-lookup", "AzureOpenAiSemanticCacheLookup");

public class LlmSemanticCacheLookupDecompiler()
    : BaseSemanticCacheLookupDecompiler("llm-semantic-cache-lookup", "LlmSemanticCacheLookup");

public abstract class BaseSemanticCacheLookupDecompiler : IPolicyDecompiler
{
    public string PolicyName { get; }
    private readonly string _methodName;

    protected BaseSemanticCacheLookupDecompiler(string policyName, string methodName)
    {
        PolicyName = policyName;
        _methodName = methodName;
    }

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        var scoreValue = element.Attribute("score-threshold")?.Value ?? "0";
        if (context.IsExpression(scoreValue))
            props.Add($"ScoreThreshold = {context.HandleValue(scoreValue, "ScoreThreshold", "decimal")}");
        else
            props.Add($"ScoreThreshold = {scoreValue}");

        context.AddRequiredExprStringProp(props, element, "embeddings-backend-id", "EmbeddingsBackendId");
        context.AddRequiredExprStringProp(props, element, "embeddings-backend-auth", "EmbeddingsBackendAuth");
        context.AddOptionalBoolExprProp(props, element, "ignore-system-messages", "IgnoreSystemMessages");
        context.AddOptionalUIntProp(props, element, "max-message-count", "MaxMessageCount");

        var varyByElements = element.Elements("vary-by").ToList();
        if (varyByElements.Count > 0)
        {
            var values = varyByElements.Select(e => PolicyDecompilerContext.Literal(PolicyDecompilerContext.GetElementText(e)));
            props.Add($"VaryBy = new[] {{ {string.Join(", ", values)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, _methodName, "SemanticCacheLookupConfig", props);
    }
}
