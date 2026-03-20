// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class PublishToDarpDecompiler : IPolicyDecompiler
{
    public string PolicyName => "publish-to-darp";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddRequiredStringProp(props, element, "topic", "Topic");

        var content = PolicyDecompilerContext.GetElementText(element);
        props.Add($"Content = {context.HandleValue(content, "Content")}");

        context.AddOptionalStringProp(props, element, "pub-sub-name", "PubSubName");
        context.AddOptionalBoolProp(props, element, "ignore-error", "IgnoreError");
        context.AddOptionalStringProp(props, element, "response-variable-name", "ResponseVariableName");
        context.AddOptionalIntProp(props, element, "timeout", "Timeout");
        context.AddOptionalStringProp(props, element, "template", "Template");
        context.AddOptionalStringProp(props, element, "content-type", "ContentType");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "PublishToDarp", "PublishToDarpConfig", props);
    }
}
