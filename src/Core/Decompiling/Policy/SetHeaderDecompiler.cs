// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetHeaderDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-header";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var name = element.Attribute("name")?.Value ?? "";
        var existsAction = element.Attribute("exists-action")?.Value ?? "override";
        var values = element.Elements("value").ToList();

        string methodName = existsAction switch
        {
            "append" => "AppendHeader",
            "skip" => "SetHeaderIfNotExist",
            "delete" => "RemoveHeader",
            _ => "SetHeader",
        };

        var nameExpr = context.HandleValue(name, "HeaderName");
        if (existsAction == "delete" || values.Count == 0)
        {
            writer.AppendLine($"{prefix}{methodName}({nameExpr});");
            return;
        }

        var valueExprs = values.Select(v =>
            context.HandleValue(PolicyDecompilerContext.GetElementTextOrValue(v), "HeaderValue")).ToList();
        writer.AppendLine($"{prefix}{methodName}({nameExpr}, {string.Join(", ", valueExprs)});");
    }
}
