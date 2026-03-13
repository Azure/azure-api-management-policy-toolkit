// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class EmitMetricDecompiler : IPolicyDecompiler
{
    public string PolicyName => "emit-metric";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "name", "Name");

        context.AddOptionalDoubleProp(props, element, "value", "Value");

        context.AddOptionalStringProp(props, element, "namespace", "Namespace");

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

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "EmitMetric", "EmitMetricConfig", props);
    }
}
