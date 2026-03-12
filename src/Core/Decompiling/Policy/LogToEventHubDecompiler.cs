// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class LogToEventHubDecompiler : IPolicyDecompiler
{
    public string PolicyName => "log-to-eventhub";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredExprStringProp(props, element, "logger-id", "LoggerId");

        var content = PolicyDecompilerContext.GetElementText(element);
        props.Add($"Value = {context.HandleValue(content, "EventHubValue")}");

        context.AddOptionalStringProp(props, element, "partition-id", "PartitionId");
        context.AddOptionalStringProp(props, element, "partition-key", "PartitionKey");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "LogToEventHub", "LogToEventHubConfig", props);
    }
}
