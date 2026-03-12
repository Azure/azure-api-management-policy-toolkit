// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class RewriteUriDecompiler : IPolicyDecompiler
{
    public string PolicyName => "rewrite-uri";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var template = element.Attribute("template")?.Value ?? "";
        var copyUnmatched = element.Attribute("copy-unmatched-params")?.Value;
        var templateExpr = context.HandleValue(template, "RewriteTemplate");

        if (copyUnmatched != null)
        {
            var copyExpr = context.HandleBoolValue(copyUnmatched, "CopyUnmatched");
            writer.AppendLine($"{prefix}RewriteUri({templateExpr}, {copyExpr});");
        }
        else
        {
            writer.AppendLine($"{prefix}RewriteUri({templateExpr});");
        }
    }
}
