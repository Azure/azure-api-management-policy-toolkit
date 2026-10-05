// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetQueryParameterDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-query-parameter";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var name = element.Attribute("name")?.Value ?? "";
        var existsAction = element.Attribute("exists-action")?.Value ?? "override";
        var values = element.Elements("value").ToList();

        // An exists-action that isn't one of the known literals (e.g. an expression), or a set without
        // values, has no method form; the element is kept as written.
        if (existsAction is not ("override" or "append" or "skip" or "delete") ||
            (existsAction != "delete" && values.Count == 0))
        {
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }

        string methodName = existsAction switch
        {
            "append" => "AppendQueryParameter",
            "skip" => "SetQueryParameterIfNotExist",
            "delete" => "RemoveQueryParameter",
            _ => "SetQueryParameter",
        };

        var nameExpr = context.HandleValue(name, "QueryParamName");
        if (existsAction == "delete" || values.Count == 0)
        {
            writer.AppendLine($"{prefix}{methodName}({nameExpr});");
            return;
        }

        var valueExprs = values.Select(v =>
            context.HandleValue(PolicyDecompilerContext.GetElementTextOrValue(v), "QueryParamValue")).ToList();
        writer.AppendLine($"{prefix}{methodName}({nameExpr}, {string.Join(", ", valueExprs)});");
    }
}
