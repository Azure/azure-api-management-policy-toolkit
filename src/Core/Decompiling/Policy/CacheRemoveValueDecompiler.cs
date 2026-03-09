// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CacheRemoveValueDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cache-remove-value";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "key", "Key");
        context.AddOptionalStringProp(props, element, "caching-type", "CachingType");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "CacheRemoveValue", "CacheRemoveValueConfig", props);
    }
}
