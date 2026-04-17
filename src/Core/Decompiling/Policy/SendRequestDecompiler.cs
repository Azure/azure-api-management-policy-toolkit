// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SendRequestDecompiler : IPolicyDecompiler
{
    public string PolicyName => "send-request";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "response-variable-name", "ResponseVariableName");
        context.AddOptionalStringProp(props, element, "mode", "Mode");
        context.AddOptionalIntProp(props, element, "timeout", "Timeout");
        context.AddOptionalBoolProp(props, element, "ignore-error", "IgnoreError");
        context.AddOptionalBoolProp(props, element, "buffer-response", "BufferResponse");
        context.AddOptionalBoolProp(props, element, "fail-on-error-status-code", "FailOnErrorStatusCode");

        var url = element.Element("set-url");
        if (url != null)
        {
            var urlValue = PolicyDecompilerContext.GetElementText(url);
            props.Add($"Url = {context.HandleValue(urlValue, "RequestUrl")}");
        }

        var method = element.Element("set-method");
        if (method != null)
        {
            props.Add($"Method = {PolicyDecompilerContext.Literal(PolicyDecompilerContext.GetElementText(method))}");
        }

        var headers = element.Elements("set-header").ToList();
        if (headers.Count > 0)
        {
            var headerConfigs = headers.Select(context.BuildHeaderConfigString).ToList();
            props.Add($"Headers = new HeaderConfig[]\n            {{\n                {string.Join(",\n                ", headerConfigs)},\n            }}");
        }

        var body = element.Element("set-body");
        if (body != null)
        {
            props.Add(context.BuildBodyConfigProperty(body));
        }

        SendRequestDecompilerHelper.EmitAuthentication(context, element, props);
        SendRequestDecompilerHelper.EmitProxy(element, props);

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "SendRequest", "SendRequestConfig", props);
    }
}
