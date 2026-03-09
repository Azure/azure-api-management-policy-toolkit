// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class RateLimitDecompiler : IPolicyDecompiler
{
    public string PolicyName => "rate-limit";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        if (element.HasElements)
        {
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }

        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredIntProp(props, element, "calls", "Calls");
        context.AddRequiredIntProp(props, element, "renewal-period", "RenewalPeriod");
        context.AddOptionalStringProp(props, element, "retry-after-header-name", "RetryAfterHeaderName");
        context.AddOptionalStringProp(props, element, "retry-after-variable-name", "RetryAfterVariableName");
        context.AddOptionalStringProp(props, element, "remaining-calls-header-name", "RemainingCallsHeaderName");
        context.AddOptionalStringProp(props, element, "remaining-calls-variable-name", "RemainingCallsVariableName");
        context.AddOptionalStringProp(props, element, "total-calls-header-name", "TotalCallsHeaderName");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "RateLimit", "RateLimitConfig", props);
    }
}
