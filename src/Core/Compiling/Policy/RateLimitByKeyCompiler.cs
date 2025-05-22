// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class RateLimitByKeyCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.RateLimitByKey);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<RateLimitByKeyConfig>(context, "rate-limit-by-key", out var values))
        {
            return;
        }

        var element = new XElement("rate-limit-by-key");

        if (!element.AddAttribute(values, nameof(RateLimitByKeyConfig.Calls), "calls"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "rate-limit-by-key",
                nameof(RateLimitByKeyConfig.Calls)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(RateLimitByKeyConfig.RenewalPeriod), "renewal-period"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "rate-limit-by-key",
                nameof(RateLimitByKeyConfig.RenewalPeriod)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(RateLimitByKeyConfig.CounterKey), "counter-key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "rate-limit-by-key",
                nameof(RateLimitByKeyConfig.CounterKey)
            ));
            return;
        }

        element.AddAttribute(values, nameof(RateLimitByKeyConfig.IncrementCondition), "increment-condition");
        element.AddAttribute(values, nameof(RateLimitByKeyConfig.IncrementCount), "increment-count");
        element.AddAttribute(values, nameof(RateLimitByKeyConfig.RetryAfterHeaderName), "retry-after-header-name");
        element.AddAttribute(values, nameof(RateLimitByKeyConfig.RetryAfterVariableName), "retry-after-variable-name");
        element.AddAttribute(values, nameof(RateLimitByKeyConfig.RemainingCallsHeaderName),
            "remaining-calls-header-name");
        element.AddAttribute(values, nameof(RateLimitByKeyConfig.RemainingCallsVariableName),
            "remaining-calls-variable-name");
        element.AddAttribute(values, nameof(RateLimitByKeyConfig.TotalCallsHeaderName), "total-calls-header-name");

        context.AddPolicy(element);
    }
}