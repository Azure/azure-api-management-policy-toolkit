// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class FindAndReplaceDecompiler : IPolicyDecompiler
{
    public string PolicyName => "find-and-replace";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var from = element.Attribute("from")?.Value ?? "";
        var to = element.Attribute("to")?.Value ?? "";
        var fromExpr = context.HandleValue(from, "FindFrom");
        var toExpr = context.HandleValue(to, "ReplaceTo");
        writer.AppendLine($"{prefix}FindAndReplace({fromExpr}, {toExpr});");
    }
}
