// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class JsonToXmlDecompiler : IPolicyDecompiler
{
    public string PolicyName => "json-to-xml";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var nsSep = element.Attribute("namespace-separator")?.Value;
        var nsSepIsCode = nsSep != null &&
                          (context.IsExpression(nsSep) || PolicyDecompilerContext.ContainsNamedValueToken(nsSep));
        if (nsSep != null && !nsSepIsCode && nsSep.Length != 1)
        {
            // The config's NamespaceSeparator is a single char; any other literal has no typed representation.
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }

        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "apply", "Apply");
        context.AddOptionalBoolProp(props, element, "consider-accept-header", "ConsiderAcceptHeader");
        context.AddOptionalBoolProp(props, element, "parse-date", "ParseDate");
        if (nsSepIsCode)
        {
            props.Add($"NamespaceSeparator = {context.HandleValue(nsSep!, "NamespaceSeparator", "char")}");
        }
        else if (nsSep != null)
        {
            props.Add($"NamespaceSeparator = '{PolicyDecompilerContext.EscapeChar(nsSep[0])}'");
        }
        context.AddOptionalStringProp(props, element, "namespace-prefix", "NamespacePrefix");
        context.AddOptionalStringProp(props, element, "attribute-block-name", "AttributeBlockName");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "JsonToXml", "JsonToXmlConfig", props);
    }
}
