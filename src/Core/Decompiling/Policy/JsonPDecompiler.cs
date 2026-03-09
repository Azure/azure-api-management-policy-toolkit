// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class JsonPDecompiler : IPolicyDecompiler
{
    public string PolicyName => "jsonp";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var callback = element.Attribute("callback-parameter-name")?.Value ?? "";
        var callbackExpr = context.HandleValue(callback, "JsonPCallback");
        writer.AppendLine($"{prefix}JsonP({callbackExpr});");
    }
}
