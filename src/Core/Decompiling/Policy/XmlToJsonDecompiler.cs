// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class XmlToJsonDecompiler : IPolicyDecompiler
{
    public string PolicyName => "xml-to-json";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "kind", "Kind");
        context.AddRequiredStringProp(props, element, "apply", "Apply");
        context.AddOptionalBoolProp(props, element, "consider-accept-header", "ConsiderAcceptHeader");
        context.AddOptionalBoolProp(props, element, "always-array-child-elements", "AlwaysArrayChildElements");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "XmlToJson", "XmlToJsonConfig", props);
    }
}
