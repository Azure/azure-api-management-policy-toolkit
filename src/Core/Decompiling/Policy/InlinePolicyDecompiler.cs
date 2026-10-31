// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class InlinePolicyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "__fallback__";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        // Raw policy text: expressions are written as code, not XML-escaped, which is what InlinePolicy expects.
        var xmlString = PolicyDecompilerContext.ToRawXml(element);
        var escaped = PolicyDecompilerContext.EscapeStringForVerbatim(xmlString);
        writer.AppendLine($"{contextVar}.InlinePolicy(@\"{escaped}\");");
    }
}
