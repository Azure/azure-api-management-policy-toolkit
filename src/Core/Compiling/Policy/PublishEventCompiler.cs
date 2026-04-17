// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class PublishEventCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IOutboundContext.PublishEvent);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<PublishEventConfig>(context, "publish-event", out var values))
        {
            return;
        }

        var element = new XElement("publish-event");

        if (!values.TryGetValue(nameof(PublishEventConfig.Subscriptions), out var subscriptionsInitializer))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "publish-event",
                nameof(PublishEventConfig.Subscriptions)
            ));
            return;
        }

        var subscriptions = subscriptionsInitializer.UnnamedValues ?? Array.Empty<InitializerValue>();
        if (subscriptions.Count == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                subscriptionsInitializer.Node.GetLocation(),
                "publish-event",
                nameof(PublishEventConfig.Subscriptions)
            ));
            return;
        }

        var targetsElement = new XElement("targets");

        foreach (var subscription in subscriptions)
        {
            if (!subscription.TryGetValues<GraphqlSubscriptionConfig>(out var result))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    subscription.Node.GetLocation(),
                    "publish-event.graphql-subscriptions",
                    nameof(GraphqlSubscriptionConfig)
                ));
                continue;
            }

            var subscriptionElement = new XElement("graphql-subscriptions");
            if (!subscriptionElement.AddAttribute(result, nameof(GraphqlSubscriptionConfig.Id), "id"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    node.GetLocation(),
                    "publish-event.graphql-subscriptions",
                    nameof(GraphqlSubscriptionConfig.Id)
                ));
                continue;
            }

            targetsElement.Add(subscriptionElement);
        }

        element.Add(targetsElement);
        context.AddPolicy(element);
    }
}
