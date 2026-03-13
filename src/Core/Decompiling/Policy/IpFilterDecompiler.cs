// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class IpFilterDecompiler : IPolicyDecompiler
{
    public string PolicyName => "ip-filter";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "action", "Action");

        var addresses = element.Elements("address").Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (addresses.Count > 0)
        {
            props.Add($"Addresses = new[] {{ {string.Join(", ", addresses.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var ranges = element.Elements("address-range").ToList();
        if (ranges.Count > 0)
        {
            var rangeConfigs = ranges.Select(r =>
            {
                var from = r.Attribute("from")?.Value ?? "";
                var to = r.Attribute("to")?.Value ?? "";
                return $"new AddressRange {{ From = {PolicyDecompilerContext.Literal(from)}, To = {PolicyDecompilerContext.Literal(to)} }}";
            });
            props.Add($"AddressRanges = new AddressRange[] {{ {string.Join(", ", rangeConfigs)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "IpFilter", "IpFilterConfig", props);
    }
}
