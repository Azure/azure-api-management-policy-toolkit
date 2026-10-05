// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class LlmContentSafetyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "llm-content-safety";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddRequiredStringProp(props, element, "backend-id", "BackendId");
        context.AddOptionalBoolProp(props, element, "shield-prompt", "ShieldPrompt");
        context.AddOptionalBoolProp(props, element, "enforce-on-completions", "EnforceOnCompletions");
        context.AddOptionalIntProp(props, element, "window-size", "WindowSize");
        context.AddOptionalIntProp(props, element, "window-overlap-size", "WindowOverlapSize");

        var categoriesElement = element.Element("categories");
        if (categoriesElement != null)
        {
            var catProps = new List<string>();

            var outputType = categoriesElement.Attribute("output-type")?.Value;
            if (outputType != null)
            {
                catProps.Add($"OutputType = {PolicyDecompilerContext.Literal(outputType)}");
            }

            var categories = categoriesElement.Elements("category").ToList();
            if (categories.Count > 0)
            {
                var categoryConfigs = categories.Select(c =>
                {
                    var name = c.Attribute("name")?.Value ?? "";
                    var threshold = context.HandleIntValue(c.Attribute("threshold")?.Value ?? "0", "Threshold");
                    return $"new ContentSafetyCategory {{ Name = {PolicyDecompilerContext.Literal(name)}, Threshold = {threshold} }}";
                });
                catProps.Add($"Categories = new ContentSafetyCategory[] {{ {string.Join(", ", categoryConfigs)} }}");
            }

            props.Add($"Categories = new ContentSafetyCategories {{ {string.Join(", ", catProps)} }}");
        }

        var blockListsElement = element.Element("blocklists") ?? element.Element("block-lists");
        if (blockListsElement != null)
        {
            var ids = blockListsElement.Elements("id")
                .Select(e => PolicyDecompilerContext.Literal(PolicyDecompilerContext.GetElementText(e)))
                .ToList();
            if (ids.Count > 0)
            {
                props.Add($"BlockLists = new ContentSafetyBlockLists {{ Ids = new[] {{ {string.Join(", ", ids)} }} }}");
            }
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "LlmContentSafety", "LlmContentSafetyConfig", props);
    }
}
