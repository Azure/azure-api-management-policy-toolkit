// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CacheLookupDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cache-lookup";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredBoolProp(props, element, "vary-by-developer", "VaryByDeveloper");
        context.AddRequiredBoolProp(props, element, "vary-by-developer-groups", "VaryByDeveloperGroups");
        context.AddOptionalStringProp(props, element, "caching-type", "CachingType");
        context.AddOptionalStringProp(props, element, "downstream-caching-type", "DownstreamCachingType");
        context.AddOptionalBoolProp(props, element, "must-revalidate", "MustRevalidate");
        context.AddOptionalBoolProp(props, element, "allow-private-response-caching", "AllowPrivateResponseCaching");

        var varyByHeaders = element.Elements("vary-by-header").Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (varyByHeaders.Count > 0)
        {
            props.Add($"VaryByHeaders = new[] {{ {string.Join(", ", varyByHeaders.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var varyByQuery = element.Elements("vary-by-query-parameter").Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (varyByQuery.Count > 0)
        {
            props.Add($"VaryByQueryParameters = new[] {{ {string.Join(", ", varyByQuery.Select(PolicyDecompilerContext.Literal))} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "CacheLookup", "CacheLookupConfig", props);
    }
}
