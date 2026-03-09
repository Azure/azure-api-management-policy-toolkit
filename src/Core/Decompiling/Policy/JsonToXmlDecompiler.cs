// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class JsonToXmlDecompiler : IPolicyDecompiler
{
    public string PolicyName => "json-to-xml";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "apply", "Apply");
        context.AddOptionalBoolProp(props, element, "consider-accept-header", "ConsiderAcceptHeader");
        context.AddOptionalBoolProp(props, element, "parse-date", "ParseDate");
        var nsSep = element.Attribute("namespace-separator")?.Value;
        if (nsSep != null && nsSep.Length > 0)
        {
            props.Add($"NamespaceSeparator = '{PolicyDecompilerContext.EscapeChar(nsSep[0])}'");
        }
        context.AddOptionalStringProp(props, element, "namespace-prefix", "NamespacePrefix");
        context.AddOptionalStringProp(props, element, "attribute-block-name", "AttributeBlockName");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "JsonToXml", "JsonToXmlConfig", props);
    }
}
