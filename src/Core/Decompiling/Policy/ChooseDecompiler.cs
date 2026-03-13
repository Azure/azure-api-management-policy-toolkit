// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ChooseDecompiler : IPolicyDecompiler
{
    public string PolicyName => "choose";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var whens = element.Elements("when").ToList();
        var otherwise = element.Element("otherwise");

        for (int i = 0; i < whens.Count; i++)
        {
            var when = whens[i];
            var condition = when.Attribute("condition")?.Value ?? "true";
            var conditionExpr = context.HandleConditionExpression(condition, "Condition");

            if (i == 0)
            {
                writer.AppendLine($"if ({conditionExpr})");
            }
            else
            {
                writer.AppendLine($"else if ({conditionExpr})");
            }
            writer.AppendLine("{");
            writer.IncreaseIndent();
            context.EmitPolicies(writer, when.Elements(), contextVar);
            writer.DecreaseIndent();
            writer.AppendLine("}");
        }

        if (otherwise != null)
        {
            writer.AppendLine("else");
            writer.AppendLine("{");
            writer.IncreaseIndent();
            context.EmitPolicies(writer, otherwise.Elements(), contextVar);
            writer.DecreaseIndent();
            writer.AppendLine("}");
        }
    }
}
