// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class LlmTokenLimitCompiler()
    : BaseTokenLimitCompiler("llm-token-limit", nameof(IInboundContext.LlmTokenLimit));

public class AzureOpenAiTokenLimitCompiler()
    : BaseTokenLimitCompiler("azure-openai-token-limit", nameof(IInboundContext.AzureOpenAiTokenLimit));

public abstract class BaseTokenLimitCompiler : IMethodPolicyHandler
{
    private readonly string _policyName;

    protected BaseTokenLimitCompiler(string policyName, string methodName)
    {
        _policyName = policyName;
        MethodName = methodName;
    }

    public string MethodName { get; }

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<TokenLimitConfig>(context, _policyName, out var values))
        {
            return;
        }

        var element = new XElement(_policyName);

        // Add required attributes
        if (!element.AddAttribute(values, nameof(TokenLimitConfig.CounterKey), "counter-key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                _policyName,
                nameof(TokenLimitConfig.CounterKey)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(TokenLimitConfig.EstimatePromptToken), "estimate-prompt-token"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                _policyName,
                nameof(TokenLimitConfig.EstimatePromptToken)
            ));
            return;
        }

        var tokensPerMinuteAdded =
            element.AddAttribute(values, nameof(TokenLimitConfig.TokensPerMinute), "tokens-per-minute");
        var quotaAdded = element.AddAttribute(values, nameof(TokenLimitConfig.TokenQuota), "token-quota");

        if (tokensPerMinuteAdded == quotaAdded)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                node.GetLocation(),
                "llm-token-limit",
                nameof(TokenLimitConfig.TokensPerMinute),
                nameof(TokenLimitConfig.TokenQuota)
            ));
            return;
        }

        element.AddAttribute(values, nameof(TokenLimitConfig.TokenQuotaPeriod), "token-quota-period");
        element.AddAttribute(values, nameof(TokenLimitConfig.RetryAfterHeaderName), "retry-after-header-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.RetryAfterVariableName), "retry-after-variable-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.RemainingQuotaTokensHeaderName),
            "remaining-quota-tokens-header-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.RemainingQuotaTokensVariableName),
            "remaining-quota-tokens-variable-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.RemainingTokensHeaderName),
            "remaining-tokens-header-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.RemainingTokensVariableName),
            "remaining-tokens-variable-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.TokensConsumedHeaderName), "tokens-consumed-header-name");
        element.AddAttribute(values, nameof(TokenLimitConfig.TokensConsumedVariableName),
            "tokens-consumed-variable-name");

        context.AddPolicy(element);
    }
}