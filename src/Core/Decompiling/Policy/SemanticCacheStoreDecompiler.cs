// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AzureOpenAiSemanticCacheStoreDecompiler()
    : BaseSemanticCacheStoreDecompiler("azure-openai-semantic-cache-store", "AzureOpenAiSemanticCacheStore");

public class LlmSemanticCacheStoreDecompiler()
    : BaseSemanticCacheStoreDecompiler("llm-semantic-cache-store", "LlmSemanticCacheStore");

public abstract class BaseSemanticCacheStoreDecompiler : IPolicyDecompiler
{
    public string PolicyName { get; }
    private readonly string _methodName;

    protected BaseSemanticCacheStoreDecompiler(string policyName, string methodName)
    {
        PolicyName = policyName;
        _methodName = methodName;
    }

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var duration = element.Attribute("duration")?.Value ?? "0";
        var durationExpr = context.HandleUintValue(duration, "Duration");
        writer.AppendLine($"{prefix}{_methodName}({durationExpr});");
    }
}
