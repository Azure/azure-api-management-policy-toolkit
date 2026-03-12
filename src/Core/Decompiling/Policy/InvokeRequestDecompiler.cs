// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class InvokeRequestDecompiler : IPolicyDecompiler
{
    public string PolicyName => "invoke-request";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "method", "Method");
        context.AddOptionalStringProp(props, element, "url", "Url");
        context.AddOptionalStringProp(props, element, "backend-id", "BackendId");
        context.AddOptionalStringProp(props, element, "response-variable-name", "ResponseVariableName");

        var headers = element.Elements("header").ToList();
        if (headers.Count > 0)
        {
            var headerConfigs = headers.Select(context.BuildInvokeRequestHeaderConfigString).ToList();
            props.Add($"Headers = new HeaderConfig[]\n            {{\n                {string.Join(",\n                ", headerConfigs)},\n            }}");
        }

        var body = element.Element("body");
        if (body != null)
        {
            props.Add($"Body = new BodyConfig {{ Content = {context.HandleValue(PolicyDecompilerContext.GetElementText(body), "BodyContent")} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "InvokeRequest", "InvokeRequestConfig", props);
    }
}
