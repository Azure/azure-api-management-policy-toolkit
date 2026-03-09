// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class BaseDecompiler : IPolicyDecompiler
{
    public string PolicyName => "base";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        if (element.HasElements)
        {
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }
        PolicyDecompilerContext.EmitSimpleCall(writer, element, contextVar, "Base");
    }
}
