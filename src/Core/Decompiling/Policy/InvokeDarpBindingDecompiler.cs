// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class InvokeDarpBindingDecompiler : IPolicyDecompiler
{
    public string PolicyName => "invoke-darp-binding";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddRequiredStringProp(props, element, "name", "Name");
        context.AddOptionalStringProp(props, element, "operation", "Operation");
        context.AddOptionalBoolProp(props, element, "ignore-error", "IgnoreError");
        context.AddOptionalStringProp(props, element, "response-variable-name", "ResponseVariableName");
        context.AddOptionalIntProp(props, element, "timeout", "Timeout");
        context.AddOptionalStringProp(props, element, "template", "Template");
        context.AddOptionalStringProp(props, element, "content-type", "ContentType");

        var metadataEl = element.Element("metadata");
        if (metadataEl != null)
        {
            var items = metadataEl.Elements("item").Select(item =>
            {
                var itemProps = new List<string>
                {
                    $"Key = {PolicyDecompilerContext.Literal(item.Attribute("key")?.Value ?? "")}",
                    $"Value = {context.HandleValue(PolicyDecompilerContext.GetElementText(item), "MetaDataValue")}"
                };
                return $"new DarpMetaData {{ {string.Join(", ", itemProps)} }}";
            });
            props.Add($"MetaData = new DarpMetaData[] {{ {string.Join(", ", items)} }}");
        }

        var dataEl = element.Element("data");
        if (dataEl != null)
        {
            var dataContent = PolicyDecompilerContext.GetElementText(dataEl);
            props.Add($"Data = {context.HandleValue(dataContent, "Data")}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "InvokeDarpBinding", "InvokeDarpBindingConfig", props);
    }
}
