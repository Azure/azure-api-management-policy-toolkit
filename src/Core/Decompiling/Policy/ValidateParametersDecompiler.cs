// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateParametersDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-parameters";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "specified-parameter-action", "SpecifiedParameterAction");
        context.AddRequiredStringProp(props, element, "unspecified-parameter-action", "UnspecifiedParameterAction");
        context.AddOptionalStringProp(props, element, "errors-variable-name", "ErrorsVariableName");

        AddParameterGroup(context, props, element, "headers", "Headers", "ValidateHeaderParameters");
        AddParameterGroup(context, props, element, "query", "Query", "ValidateQueryParameters");
        AddParameterGroup(context, props, element, "path", "Path", "ValidatePathParameters");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateParameters", "ValidateParametersConfig", props);
    }

    private static void AddParameterGroup(
        PolicyDecompilerContext context, List<string> props, XElement element,
        string xmlName, string propName, string configTypeName)
    {
        var group = element.Element(xmlName);
        if (group == null)
        {
            return;
        }

        var groupProps = new List<string>();
        context.AddOptionalStringProp(groupProps, group, "specified-parameter-action", "SpecifiedParameterAction");
        context.AddOptionalStringProp(groupProps, group, "unspecified-parameter-action", "UnspecifiedParameterAction");

        var parameters = group.Elements("parameter").ToList();
        if (parameters.Count > 0)
        {
            var parameterConfigs = parameters.Select(p =>
            {
                var parameterProps = new List<string>
                {
                    $"Name = {PolicyDecompilerContext.Literal(p.Attribute("name")?.Value ?? "")}"
                };
                context.AddOptionalStringProp(parameterProps, p, "action", "Action");
                return $"new ValidateParameter {{ {string.Join(", ", parameterProps)} }}";
            });
            groupProps.Add($"Parameters = new ValidateParameter[] {{ {string.Join(", ", parameterConfigs)} }}");
        }

        props.Add($"{propName} = new {configTypeName} {{ {string.Join(", ", groupProps)} }}");
    }
}
