// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateGraphqlRequestDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-graphql-request";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddOptionalStringProp(props, element, "error-variable-name", "ErrorVariableName");
        context.AddOptionalIntProp(props, element, "max-depth", "MaxDepth");
        context.AddOptionalIntProp(props, element, "max-size", "MaxSize");
        context.AddOptionalIntProp(props, element, "max-total-depth", "MaxTotalDepth");
        context.AddOptionalIntProp(props, element, "max-complexity", "MaxComplexity");

        var authorizeElement = element.Element("authorize");
        if (authorizeElement != null)
        {
            var ruleElements = authorizeElement.Elements("rule").ToList();
            var ruleLines = new List<string>();
            foreach (var ruleEl in ruleElements)
            {
                var ruleProps = new List<string>();
                var path = ruleEl.Attribute("path")?.Value;
                if (path != null)
                {
                    ruleProps.Add($"Path = {PolicyDecompilerContext.Literal(path)}");
                }
                var action = ruleEl.Attribute("action")?.Value;
                if (action != null)
                {
                    ruleProps.Add($"Action = {PolicyDecompilerContext.Literal(action)}");
                }
                ruleLines.Add($"new {nameof(AuthorizeRuleConfig)} {{ {string.Join(", ", ruleProps)} }}");
            }

            var rulesArray = $"new {nameof(AuthorizeRuleConfig)}[] {{ {string.Join(", ", ruleLines)} }}";
            var authorizeInit = $"new {nameof(AuthorizeConfig)} {{ Rules = {rulesArray} }}";
            props.Add($"Authorize = {authorizeInit}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateGraphqlRequest", "ValidateGraphqlRequestConfig", props);
    }
}
