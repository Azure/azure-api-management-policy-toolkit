// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class PublishEventDecompiler : IPolicyDecompiler
{
    public string PolicyName => "publish-event";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        var targetsElement = element.Element("targets");
        if (targetsElement != null)
        {
            var subscriptions = targetsElement.Elements("graphql-subscriptions").ToList();
            if (subscriptions.Count > 0)
            {
                var items = new List<string>();
                foreach (var sub in subscriptions)
                {
                    var id = sub.Attribute("id")?.Value;
                    if (id != null)
                    {
                        items.Add($"new GraphqlSubscriptionConfig {{ Id = \"{id}\" }}");
                    }
                }

                props.Add($"Subscriptions = [{string.Join(", ", items)}]");
            }
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "PublishEvent", "PublishEventConfig", props);
    }
}
