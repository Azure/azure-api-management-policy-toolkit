// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SetBackendServiceCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SetBackendService);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<SetBackendServiceConfig>(context, "set-backend-service", out var values))
        {
            return;
        }

        var element = new XElement("set-backend-service");

        var baseUrlDefined = element.AddAttribute(values, nameof(SetBackendServiceConfig.BaseUrl), "base-url");
        var backendIdDefined = element.AddAttribute(values, nameof(SetBackendServiceConfig.BackendId), "backend-id");
        if (!(baseUrlDefined ^ backendIdDefined))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                node.GetLocation(),
                "set-backend-service",
                nameof(SetBackendServiceConfig.BaseUrl),
                nameof(SetBackendServiceConfig.BackendId)
            ));
            return;
        }

        element.AddAttribute(values, nameof(SetBackendServiceConfig.SfResolveCondition), "sf-resolve-condition");
        element.AddAttribute(values, nameof(SetBackendServiceConfig.SfServiceInstanceName), "sf-service-instance-name");
        element.AddAttribute(values, nameof(SetBackendServiceConfig.SfPartitionKey), "sf-partition-key");
        element.AddAttribute(values, nameof(SetBackendServiceConfig.SfListenerName), "sf-listener-name");

        element.AddAttribute(values, nameof(SetBackendServiceConfig.DaprAppId), "dapr-app-id");
        element.AddAttribute(values, nameof(SetBackendServiceConfig.DaprMethod), "dapr-method");
        element.AddAttribute(values, nameof(SetBackendServiceConfig.DaprNamespace), "dapr-namespace");

        context.AddPolicy(element);
    }
}