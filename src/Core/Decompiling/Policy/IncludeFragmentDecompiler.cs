// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class IncludeFragmentDecompiler : IPolicyDecompiler
{
    public string PolicyName => "include-fragment";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var fragmentId = element.Attribute("fragment-id")?.Value ?? "";
        writer.AppendLine($"{prefix}IncludeFragment({PolicyDecompilerContext.Literal(fragmentId)});");
    }
}
