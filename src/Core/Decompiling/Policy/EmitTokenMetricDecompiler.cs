// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AzureOpenAiEmitTokenMetricDecompiler()
    : BaseEmitTokenMetricDecompiler("azure-openai-emit-token-metric", "AzureOpenAiEmitTokenMetric");

public class LlmEmitTokenMetricDecompiler()
    : BaseEmitTokenMetricDecompiler("llm-emit-token-metric", "LlmEmitTokenMetric");

public abstract class BaseEmitTokenMetricDecompiler : IPolicyDecompiler
{
    public string PolicyName { get; }
    private readonly string _methodName;

    protected BaseEmitTokenMetricDecompiler(string policyName, string methodName)
    {
        PolicyName = policyName;
        _methodName = methodName;
    }

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddOptionalExprStringProp(props, element, "namespace", "Namespace");

        var dimensions = element.Elements("dimension").ToList();
        if (dimensions.Count > 0)
        {
            var dimConfigs = dimensions.Select(d =>
            {
                var dimProps = new List<string> { $"Name = {PolicyDecompilerContext.Literal(d.Attribute("name")?.Value ?? "")}" };
                var val = d.Attribute("value")?.Value;
                if (val != null) dimProps.Add($"Value = {PolicyDecompilerContext.Literal(val)}");
                return $"new MetricDimensionConfig {{ {string.Join(", ", dimProps)} }}";
            });
            props.Add($"Dimensions = new MetricDimensionConfig[] {{ {string.Join(", ", dimConfigs)} }}");
        }
        else
        {
            props.Add("Dimensions = Array.Empty<MetricDimensionConfig>()");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, _methodName, "EmitTokenMetricConfig", props);
    }
}
