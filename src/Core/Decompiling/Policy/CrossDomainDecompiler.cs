// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CrossDomainDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cross-domain";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var innerXml = element.Elements().FirstOrDefault()?.ToString(SaveOptions.DisableFormatting) ?? "";
        writer.AppendLine($"{prefix}CrossDomain({PolicyDecompilerContext.Literal(innerXml)});");
    }
}
