// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ReturnResponseDecompiler : IPolicyDecompiler
{
    public string PolicyName => "return-response";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "response-variable-name", "ResponseVariableName");

        var status = element.Element("set-status");
        if (status != null)
        {
            var code = status.Attribute("code")?.Value ?? "0";
            var codeExpr = context.HandleIntValue(code, "ReturnStatusCode");
            var reasonAttr = status.Attribute("reason");
            if (reasonAttr != null)
            {
                var reasonExpr = context.HandleValue(reasonAttr.Value, "ReturnStatusReason");
                props.Add($"Status = new StatusConfig {{ Code = {codeExpr}, Reason = {reasonExpr} }}");
            }
            else
            {
                props.Add($"Status = new StatusConfig {{ Code = {codeExpr} }}");
            }
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

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ReturnResponse", "ReturnResponseConfig", props);
    }
}
