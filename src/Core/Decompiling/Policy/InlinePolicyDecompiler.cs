// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class InlinePolicyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "__fallback__";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var xmlString = element.ToString(SaveOptions.DisableFormatting);
        var escaped = PolicyDecompilerContext.EscapeStringForVerbatim(xmlString);
        writer.AppendLine($"{contextVar}.InlinePolicy(@\"{escaped}\");");
    }
}
