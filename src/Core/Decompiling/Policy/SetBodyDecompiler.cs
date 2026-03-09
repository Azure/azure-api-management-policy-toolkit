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
        string content;
        if (valueChild != null)
        {
            content = PolicyDecompilerContext.GetElementTextOrValue(valueChild);
        }
        else
        {
            content = PolicyDecompilerContext.GetElementText(element);
        }
        var contentExpr = context.HandleValue(content, "BodyExpression");

        var configProps = new List<string>();
        context.AddOptionalStringProp(configProps, element, "template", "Template");
        context.AddOptionalStringProp(configProps, element, "xsi-nil", "XsiNil");
        context.AddOptionalBoolProp(configProps, element, "parse-date", "ParseDate");
        if (valueChild != null)
            configProps.Add("UseValueElement = true");

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
