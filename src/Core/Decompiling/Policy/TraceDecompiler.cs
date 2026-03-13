// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class TraceDecompiler : IPolicyDecompiler
{
    public string PolicyName => "trace";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "source", "Source");

        var message = element.Element("message");
        if (message != null)
        {
            var msgText = PolicyDecompilerContext.GetElementText(message);
            props.Add($"Message = {context.HandleValue(msgText, "TraceMessage")}");
        }

        context.AddOptionalStringProp(props, element, "severity", "Severity");

        var metadata = element.Elements("metadata").ToList();
        if (metadata.Count > 0)
        {
            var items = metadata.Select(m =>
            {
                var name = m.Attribute("name")?.Value ?? "";
                var value = m.Attribute("value")?.Value ?? "";
                return $"new TraceMetadata {{ Name = {PolicyDecompilerContext.Literal(name)}, Value = {PolicyDecompilerContext.Literal(value)} }}";
            });
            props.Add($"Metadata = new TraceMetadata[] {{ {string.Join(", ", items)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "Trace", "TraceConfig", props);
    }
}
