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

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateParameters", "ValidateParametersConfig", props);
    }
}
