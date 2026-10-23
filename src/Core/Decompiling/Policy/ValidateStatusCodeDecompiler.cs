// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateStatusCodeDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-status-code";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "unspecified-status-code-action", "UnspecifiedStatusCodeAction");
        context.AddOptionalStringProp(props, element, "errors-variable-name", "ErrorsVariableName");

        var statusCodes = element.Elements("status-code").Select(statusCode =>
            $"new ValidateStatusCode {{ Code = {statusCode.Attribute("code")?.Value ?? "0"}, " +
            $"Action = {context.HandleValue(statusCode.Attribute("action")?.Value ?? "", "Action")} }}").ToList();
        if (statusCodes.Count > 0)
        {
            props.Add($"StatusCodes = new ValidateStatusCode[] {{ {string.Join(", ", statusCodes)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateStatusCode", "ValidateStatusCodeConfig", props);
    }
}
