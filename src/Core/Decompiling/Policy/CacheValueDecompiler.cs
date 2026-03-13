// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CacheValueDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cache-value";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "key", "Key");
        context.AddRequiredStringProp(props, element, "variable-name", "VariableName");
        context.AddOptionalIntPropWithEvaluator(props, element, "expires-after", "ExpiresAfter", "ExpiresAfterEvaluator");
        context.AddOptionalIntPropWithEvaluator(props, element, "refresh-after", "RefreshAfter", "RefreshAfterEvaluator");
        context.AddOptionalStringProp(props, element, "default-value", "DefaultValue");
        context.AddOptionalStringProp(props, element, "caching-type", "CachingType");

        var valueElement = element.Element("value");
        var blockElements = valueElement?.Elements() ?? element.Elements();

        writer.Append($"{prefix}CacheValue(new CacheValueConfig");
        if (props.Count <= 2 && props.All(p => p.Length < 60 && !p.Contains('\n')))
        {
            writer.AppendRaw($" {{ {string.Join(", ", props)} }}, () =>\n");
        }
        else
        {
            writer.AppendRaw("\n");
            writer.AppendLine("{");
            writer.IncreaseIndent();
            foreach (var prop in props)
                writer.AppendLine($"{prop},");
            writer.DecreaseIndent();
            writer.AppendLine("}, () =>");
        }
        writer.AppendLine("{");
        writer.IncreaseIndent();
        context.EmitPolicies(writer, blockElements, contextVar);
        writer.DecreaseIndent();
        writer.AppendLine("});");
    }
}
