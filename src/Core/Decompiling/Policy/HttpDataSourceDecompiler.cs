// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class HttpDataSourceDecompiler : IPolicyDecompiler
{
    public string PolicyName => "http-data-source";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        var httpRequest = element.Element("http-request");
        if (httpRequest != null)
        {
            var url = httpRequest.Element("set-url");
            if (url != null)
            {
                var urlValue = PolicyDecompilerContext.GetElementText(url);
                props.Add($"Url = {context.HandleValue(urlValue, "RequestUrl")}");
            }

            var method = httpRequest.Element("set-method");
            if (method != null)
            {
                props.Add($"Method = {PolicyDecompilerContext.Literal(PolicyDecompilerContext.GetElementText(method))}");
            }

            var headers = httpRequest.Elements("set-header").ToList();
            if (headers.Count > 0)
            {
                var headerConfigs = headers.Select(context.BuildHeaderConfigString).ToList();
                props.Add($"Headers = new HeaderConfig[]\n            {{\n                {string.Join(",\n                ", headerConfigs)},\n            }}");
            }

            var body = httpRequest.Element("set-body");
            if (body != null)
            {
                props.Add(context.BuildBodyConfigProperty(body));
            }

            SendRequestDecompilerHelper.EmitAuthentication(context, httpRequest, props);
        }

        var httpResponse = element.Element("http-response");
        if (httpResponse != null)
        {
            var responseHeaders = httpResponse.Elements("set-header").ToList();
            if (responseHeaders.Count > 0)
            {
                var headerConfigs = responseHeaders.Select(context.BuildHeaderConfigString).ToList();
                props.Add($"ResponseHeaders = new HeaderConfig[]\n            {{\n                {string.Join(",\n                ", headerConfigs)},\n            }}");
            }

            var responseBody = httpResponse.Element("set-body");
            if (responseBody != null)
            {
                var bodyProp = context.BuildBodyConfigProperty(responseBody);
                // BuildBodyConfigProperty returns "Body = new BodyConfig { ... }"
                // We need "ResponseBody = new BodyConfig { ... }"
                props.Add("Response" + bodyProp);
            }
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "HttpDataSource", "HttpDataSourceConfig", props);
    }
}
