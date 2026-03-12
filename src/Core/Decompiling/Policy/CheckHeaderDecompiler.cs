// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CheckHeaderDecompiler : IPolicyDecompiler
{
    public string PolicyName => "check-header";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "name", "Name");
        context.AddRequiredIntProp(props, element, "failed-check-httpcode", "FailCheckHttpCode");
        context.AddRequiredStringProp(props, element, "failed-check-error-message", "FailCheckErrorMessage");
        context.AddRequiredBoolProp(props, element, "ignore-case", "IgnoreCase");

        var values = element.Elements("value").Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (values.Count > 0)
        {
            props.Add($"Values = new[] {{ {string.Join(", ", values.Select(PolicyDecompilerContext.Literal))} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "CheckHeader", "CheckHeaderConfig", props);
    }
}
