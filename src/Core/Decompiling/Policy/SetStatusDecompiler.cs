// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetStatusDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-status";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var code = element.Attribute("code")?.Value ?? "0";
        var codeExpr = context.HandleIntValue(code, "StatusCode");
        var reasonAttr = element.Attribute("reason");
        if (reasonAttr != null)
        {
            var reasonExpr = context.HandleValue(reasonAttr.Value, "StatusReason");
            writer.AppendLine($"{prefix}SetStatus(new StatusConfig {{ Code = {codeExpr}, Reason = {reasonExpr} }});");
        }
        else
        {
            writer.AppendLine($"{prefix}SetStatus(new StatusConfig {{ Code = {codeExpr} }});");
        }
    }
}
