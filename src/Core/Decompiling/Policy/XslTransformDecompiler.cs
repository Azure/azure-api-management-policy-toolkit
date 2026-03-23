// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class XslTransformDecompiler : IPolicyDecompiler
{
    public string PolicyName => "xsl-transform";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        var stylesheet = element.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "stylesheet" || e.Name.LocalName.EndsWith(":stylesheet"));
        if (stylesheet != null)
        {
            var xslContent = stylesheet.ToString();
            props.Add($"StyleSheet = @\"{PolicyDecompilerContext.EscapeStringForVerbatim(xslContent)}\"");
        }

        var parameters = element.Elements("parameter").ToList();
        if (parameters.Count > 0)
        {
            var paramConfigs = parameters.Select(p =>
            {
                var paramProps = new List<string>
                {
                    $"Name = {PolicyDecompilerContext.Literal(p.Attribute("name")?.Value ?? "")}",
                    $"Value = {PolicyDecompilerContext.Literal(PolicyDecompilerContext.GetElementText(p))}"
                };
                return $"new XslTransformParameter {{ {string.Join(", ", paramProps)} }}";
            });
            props.Add($"Parameters = new XslTransformParameter[] {{ {string.Join(", ", paramConfigs)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "XslTransform", "XslTransformConfig", props);
    }
}
