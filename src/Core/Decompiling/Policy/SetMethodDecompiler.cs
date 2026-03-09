// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetMethodDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-method";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var method = PolicyDecompilerContext.GetElementText(element);
        writer.AppendLine($"{prefix}SetMethod({PolicyDecompilerContext.Literal(method)});");
    }
}
