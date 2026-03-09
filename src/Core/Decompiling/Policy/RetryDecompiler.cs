// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class RetryDecompiler : IPolicyDecompiler
{
    public string PolicyName => "retry";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        var conditionValue = element.Attribute("condition")?.Value;
        if (conditionValue is not null)
        {
            var conditionExpr = context.HandleBoolValue(conditionValue, "Condition");
            props.Add($"Condition = {conditionExpr}");
            if (context.IsExpression(conditionValue) || PolicyDecompilerContext.IsNamedValueToken(conditionValue))
            {
                props.Add($"ConditionEvaluator = () => {conditionExpr}");
            }
        }
        context.AddRequiredIntProp(props, element, "count", "Count");
        context.AddOptionalIntProp(props, element, "interval", "Interval");
        context.AddOptionalIntProp(props, element, "max-interval", "MaxInterval");
        context.AddOptionalIntProp(props, element, "delta", "Delta");
        context.AddOptionalBoolProp(props, element, "first-fast-retry", "FirstFastRetry");

        context.EmitConfigCallWithBlock(writer, prefix, "Retry", "RetryConfig", props, element, contextVar);
    }
}
