// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetVariableDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-variable";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var name = element.Attribute("name")?.Value ?? "";
        var value = element.Attribute("value")?.Value ?? "";
        var valueExpr = context.HandleValue(value, $"Get{PolicyDecompilerContext.ToPascalCase(name)}", "object");
        writer.AppendLine($"{prefix}SetVariable({PolicyDecompilerContext.Literal(name)}, {valueExpr});");
    }
}
