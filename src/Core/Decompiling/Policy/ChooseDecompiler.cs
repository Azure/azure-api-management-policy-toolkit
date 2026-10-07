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

        if (!CanBeIfStatement(element, whens, context))
        {
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }

        if (element.Attribute("id")?.Value is { } id)
        {
            writer.AppendLine($"{contextVar}.WithId({PolicyDecompilerContext.Literal(id)});");
        }

        for (int i = 0; i < whens.Count; i++)
        {
            var when = whens[i];
            var condition = when.Attribute("condition")!.Value;
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

    // The condition of an if statement has to be a boolean helper call (an expression or a named value)
    // or the constant true or false. The gateway accepts nothing else: it rejects "True", "1", an empty or
    // missing condition and a choose without a when. Such a document is kept as written.
    private static bool CanBeIfStatement(XElement element, List<XElement> whens, PolicyDecompilerContext context)
    {
        if (whens.Count == 0)
        {
            return false;
        }

        return whens.All(when =>
            when.Attribute("condition")?.Value is { } condition &&
            (condition.Trim() is "true" or "false" ||
             context.IsExpression(condition.Trim()) ||
             PolicyDecompilerContext.ContainsNamedValueToken(condition)));
    }
}
