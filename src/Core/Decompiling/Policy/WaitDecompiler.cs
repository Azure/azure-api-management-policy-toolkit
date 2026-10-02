// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class WaitDecompiler : IPolicyDecompiler
{
    public string PolicyName => "wait";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var waitFor = element.Attribute("for")?.Value;
        var children = element.Elements().ToList();
        if (children.Count == 0)
        {
            throw new ArgumentException("The wait policy requires at least one child policy.", nameof(element));
        }

        writer.AppendLine($"{prefix}Wait({(waitFor is null ? "null" : context.HandleValue(waitFor, "WaitFor"))},");
        writer.IncreaseIndent();
        for (var i = 0; i < children.Count; i++)
        {
            var branch = context.GenerateUniqueMethodName("waitBranch");
            writer.AppendLine($"{branch} =>");
            writer.AppendLine("{");
            writer.IncreaseIndent();
            context.EmitPolicy(writer, children[i], branch);
            writer.DecreaseIndent();
            writer.AppendLine(i == children.Count - 1 ? "});" : "},");
        }
        writer.DecreaseIndent();
    }
}
