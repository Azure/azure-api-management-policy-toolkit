// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SetBodyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "set-body";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);

        var valueChild = element.Element("value");
        string contentExpr;
        if (valueChild != null)
        {
            contentExpr = context.HandleValue(PolicyDecompilerContext.GetElementTextOrValue(valueChild), "BodyExpression");
        }
        else if (element.Nodes().Any(n => n is XElement))
        {
            // Liquid template with XML body (e.g. SOAP envelope) — serialize children verbatim.
            // Do NOT call HandleValue: Liquid {{tokens}} must not be converted to NamedValue calls.
            // Use SaveOptions.None (formatted) so element-only children (e.g. <ValidFrom>) retain
            // whitespace between their child elements, which is needed for round-trip fidelity.
            var innerXml = string.Concat(element.Nodes().Select(n => n.ToString(SaveOptions.None)));
            contentExpr = PolicyDecompilerContext.Literal(innerXml);
        }
        else
        {
            contentExpr = context.HandleValue(PolicyDecompilerContext.GetElementText(element), "BodyExpression");
        }

        var configProps = new List<string>();
        context.AddOptionalStringProp(configProps, element, "template", "Template");
        context.AddOptionalStringProp(configProps, element, "xsi-nil", "XsiNil");
        context.AddOptionalBoolProp(configProps, element, "parse-date", "ParseDate");
        if (valueChild != null)
        {
            configProps.Add("UseValueElement = true");
        }

        if (configProps.Count > 0)
        {
            var config = $"new SetBodyConfig {{ {string.Join(", ", configProps)} }}";
            writer.AppendLine($"{prefix}SetBody({contentExpr}, {config});");
        }
        else
        {
            writer.AppendLine($"{prefix}SetBody({contentExpr});");
        }
    }
}
