// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class QuotaDecompiler : IPolicyDecompiler
{
    public string PolicyName => "quota";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        if (element.HasElements)
        {
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }

        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredIntProp(props, element, "renewal-period", "RenewalPeriod");
        context.AddOptionalIntProp(props, element, "calls", "Calls");
        context.AddOptionalIntProp(props, element, "bandwidth", "Bandwidth");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "Quota", "QuotaConfig", props);
    }
}
