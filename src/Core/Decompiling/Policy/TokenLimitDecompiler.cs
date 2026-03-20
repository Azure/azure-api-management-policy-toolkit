// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AzureOpenAiTokenLimitDecompiler()
    : BaseTokenLimitDecompiler("azure-openai-token-limit", "AzureOpenAiTokenLimit");

public class LlmTokenLimitDecompiler()
    : BaseTokenLimitDecompiler("llm-token-limit", "LlmTokenLimit");

public abstract class BaseTokenLimitDecompiler : IPolicyDecompiler
{
    public string PolicyName { get; }
    private readonly string _methodName;

    protected BaseTokenLimitDecompiler(string policyName, string methodName)
    {
        PolicyName = policyName;
        _methodName = methodName;
    }

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddRequiredExprStringProp(props, element, "counter-key", "CounterKey");
        context.AddRequiredBoolExprProp(props, element, "estimate-prompt-token", "EstimatePromptToken");
        context.AddOptionalIntProp(props, element, "tokens-per-minute", "TokensPerMinute");
        context.AddOptionalIntProp(props, element, "token-quota", "TokenQuota");
        context.AddOptionalStringProp(props, element, "token-quota-period", "TokenQuotaPeriod");
        context.AddOptionalStringProp(props, element, "retry-after-header-name", "RetryAfterHeaderName");
        context.AddOptionalStringProp(props, element, "retry-after-variable-name", "RetryAfterVariableName");
        context.AddOptionalStringProp(props, element, "remaining-quota-tokens-header-name", "RemainingQuotaTokensHeaderName");
        context.AddOptionalStringProp(props, element, "remaining-quota-tokens-variable-name", "RemainingQuotaTokensVariableName");
        context.AddOptionalStringProp(props, element, "remaining-tokens-header-name", "RemainingTokensHeaderName");
        context.AddOptionalStringProp(props, element, "remaining-tokens-variable-name", "RemainingTokensVariableName");
        context.AddOptionalStringProp(props, element, "tokens-consumed-header-name", "TokensConsumedHeaderName");
        context.AddOptionalStringProp(props, element, "tokens-consumed-variable-name", "TokensConsumedVariableName");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, _methodName, "TokenLimitConfig", props);
    }
}
