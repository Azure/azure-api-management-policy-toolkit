// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateOdataRequestDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-odata-request";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "error-variable-name", "ErrorVariableName");
        context.AddOptionalStringProp(props, element, "default-odata-version", "DefaultOdataVersion");
        context.AddOptionalStringProp(props, element, "min-odata-version", "MinOdataVersion");
        context.AddOptionalStringProp(props, element, "max-odata-version", "MaxOdataVersion");
        context.AddOptionalIntProp(props, element, "max-size", "MaxSize");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateOdataRequest", "ValidateOdataRequestConfig", props);
    }
}
