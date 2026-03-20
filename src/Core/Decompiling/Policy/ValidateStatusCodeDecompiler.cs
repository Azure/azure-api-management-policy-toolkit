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
        context.AddOptionalStringProp(props, element, "error-variable-name", "ErrorVariableName");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateStatusCode", "ValidateStatusCodeConfig", props);
    }
}
