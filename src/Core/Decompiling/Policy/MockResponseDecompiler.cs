// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class MockResponseDecompiler : IPolicyDecompiler
{
    public string PolicyName => "mock-response";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalIntProp(props, element, "status-code", "StatusCode");
        context.AddOptionalStringProp(props, element, "content-type", "ContentType");
        context.AddOptionalIntProp(props, element, "index", "Index");

        if (props.Count > 0)
        {
            PolicyDecompilerContext.EmitConfigCall(writer, prefix, "MockResponse", "MockResponseConfig", props);
        }
        else
        {
            writer.AppendLine($"{prefix}MockResponse();");
        }
    }
}
