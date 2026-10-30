// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateHeadersDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-headers";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "specified-header-action", "SpecifiedHeaderAction");
        context.AddRequiredStringProp(props, element, "unspecified-header-action", "UnspecifiedHeaderAction");
        context.AddOptionalStringProp(props, element, "errors-variable-name", "ErrorsVariableName");

        var headers = element.Elements("header").ToList();
        if (headers.Count > 0)
        {
            var headerConfigs = headers.Select(h =>
            {
                var headerProps = new List<string>
                {
                    $"Name = {PolicyDecompilerContext.Literal(h.Attribute("name")?.Value ?? "")}"
                };
                context.AddOptionalStringProp(headerProps, h, "action", "Action");
                return $"new ValidateHeader {{ {string.Join(", ", headerProps)} }}";
            });
            props.Add($"Headers = new ValidateHeader[] {{ {string.Join(", ", headerConfigs)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateHeaders", "ValidateHeadersConfig", props);
    }
}
