// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class RateLimitByKeyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "rate-limit-by-key";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredIntProp(props, element, "calls", "Calls");
        context.AddRequiredIntProp(props, element, "renewal-period", "RenewalPeriod");
        context.AddRequiredExprStringProp(props, element, "counter-key", "CounterKey");
        context.AddOptionalBoolExprProp(props, element, "increment-condition", "IncrementCondition");
        context.AddOptionalIntProp(props, element, "increment-count", "IncrementCount");
        context.AddOptionalStringProp(props, element, "retry-after-header-name", "RetryAfterHeaderName");
        context.AddOptionalStringProp(props, element, "retry-after-variable-name", "RetryAfterVariableName");
        context.AddOptionalStringProp(props, element, "remaining-calls-header-name", "RemainingCallsHeaderName");
        context.AddOptionalStringProp(props, element, "remaining-calls-variable-name", "RemainingCallsVariableName");
        context.AddOptionalStringProp(props, element, "total-calls-header-name", "TotalCallsHeaderName");
        context.AddOptionalBoolProp(props, element, "increment-after-response", "IncrementAfterResponse");
        context.AddOptionalBoolProp(props, element, "flexible-retry-window", "FlexibleRetryWindow");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "RateLimitByKey", "RateLimitByKeyConfig", props);
    }
}
