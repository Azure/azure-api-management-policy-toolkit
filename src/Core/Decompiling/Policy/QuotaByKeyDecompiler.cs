// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class QuotaByKeyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "quota-by-key";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredExprStringProp(props, element, "counter-key", "CounterKey");
        context.AddRequiredIntProp(props, element, "renewal-period", "RenewalPeriod");
        context.AddOptionalIntProp(props, element, "calls", "Calls");
        context.AddOptionalIntProp(props, element, "bandwidth", "Bandwidth");
        context.AddOptionalBoolExprProp(props, element, "increment-condition", "IncrementCondition");
        context.AddOptionalIntProp(props, element, "increment-count", "IncrementCount");
        context.AddOptionalStringProp(props, element, "first-period-start", "FirstPeriodStart");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "QuotaByKey", "QuotaByKeyConfig", props);
    }
}
