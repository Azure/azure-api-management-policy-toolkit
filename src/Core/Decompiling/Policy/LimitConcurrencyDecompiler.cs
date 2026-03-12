// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class LimitConcurrencyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "limit-concurrency";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "key", "Key");
        context.AddRequiredIntProp(props, element, "max-count", "MaxCount");

        context.EmitConfigCallWithBlock(writer, prefix, "LimitConcurrency", "LimitConcurrencyConfig", props, element, contextVar);
    }
}
