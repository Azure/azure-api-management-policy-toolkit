// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ForwardRequestDecompiler : IPolicyDecompiler
{
    public string PolicyName => "forward-request";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalUIntProp(props, element, "timeout", "Timeout");
        context.AddOptionalUIntProp(props, element, "timeout-ms", "TimeoutMs");
        context.AddOptionalUIntProp(props, element, "continue-timeout", "ContinueTimeout");
        context.AddOptionalStringProp(props, element, "http-version", "HttpVersion");
        context.AddOptionalBoolProp(props, element, "follow-redirects", "FollowRedirects");
        context.AddOptionalBoolProp(props, element, "buffer-request-body", "BufferRequestBody");
        context.AddOptionalBoolProp(props, element, "buffer-response", "BufferResponse");
        context.AddOptionalBoolProp(props, element, "fail-on-error-status-code", "FailOnErrorStatusCode");

        if (props.Count > 0)
        {
            PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ForwardRequest", "ForwardRequestConfig", props);
        }
        else
        {
            writer.AppendLine($"{prefix}ForwardRequest();");
        }
    }
}
