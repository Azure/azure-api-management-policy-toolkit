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

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateHeaders", "ValidateHeadersConfig", props);
    }
}
