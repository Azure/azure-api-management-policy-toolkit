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

        writer.AppendLine($"{prefix}Wait(() =>");
        writer.AppendLine("{");
        writer.IncreaseIndent();
        context.EmitPolicies(writer, element.Elements(), contextVar);
        writer.DecreaseIndent();
        if (waitFor != null)
        {
            writer.AppendLine($"}}, {context.HandleValue(waitFor, "WaitFor")});");
        }
        else
        {
            writer.AppendLine("});");
        }
    }
}
