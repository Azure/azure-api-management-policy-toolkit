// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CacheLookupValueDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cache-lookup-value";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "key", "Key");
        context.AddRequiredStringProp(props, element, "variable-name", "VariableName");
        context.AddOptionalProp(props, element, "default-value", "DefaultValue", "object");
        context.AddOptionalStringProp(props, element, "caching-type", "CachingType");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "CacheLookupValue", "CacheLookupValueConfig", props);
    }
}
