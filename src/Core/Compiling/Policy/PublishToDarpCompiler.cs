// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class PublishToDarpCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.PublishToDarp);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<PublishToDarpConfig>(context, "publish-to-darp", out var values))
        {
            return;
        }

        var element = new XElement("publish-to-darp");

        if (!element.AddAttribute(values, nameof(PublishToDarpConfig.Topic), "topic"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "publish-to-darp",
                nameof(PublishToDarpConfig.Topic)
            ));
            return;
        }

        if (!values.TryGetValue(nameof(PublishToDarpConfig.Content), out var contentValue))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "publish-to-darp",
                nameof(PublishToDarpConfig.Content)
            ));
            return;
        }

        element.Value = contentValue.Value!;

        element.AddAttribute(values, nameof(PublishToDarpConfig.PubSubName), "pub-sub-name");
        element.AddAttribute(values, nameof(PublishToDarpConfig.IgnoreError), "ignore-error");
        element.AddAttribute(values, nameof(PublishToDarpConfig.ResponseVariableName), "response-variable-name");
        element.AddAttribute(values, nameof(PublishToDarpConfig.Timeout), "timeout");
        element.AddAttribute(values, nameof(PublishToDarpConfig.Template), "template");
        element.AddAttribute(values, nameof(PublishToDarpConfig.ContentType), "content-type");

        context.AddPolicy(element);
    }
}