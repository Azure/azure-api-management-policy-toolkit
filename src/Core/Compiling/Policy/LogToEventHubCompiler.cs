// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class LogToEventHubCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.LogToEventHub);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<LogToEventHubConfig>(context, "log-to-eventhub",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("log-to-eventhub");
        if (!element.AddAttribute(values, nameof(LogToEventHubConfig.LoggerId), "logger-id"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "log-to-eventhub",
                nameof(LogToEventHubConfig.LoggerId)
            ));
            return;
        }

        bool addedPartitionKey =
            element.AddAttribute(values, nameof(LogToEventHubConfig.PartitionKey), "partition-key");
        bool addedPartitionId = element.AddAttribute(values, nameof(LogToEventHubConfig.PartitionId), "partition-id");

        if (addedPartitionKey && addedPartitionId)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                node.GetLocation(),
                "log-to-eventhub",
                nameof(LogToEventHubConfig.PartitionKey),
                nameof(LogToEventHubConfig.PartitionId)
            ));
            return;
        }

        if (!values.TryGetValue(nameof(LogToEventHubConfig.Value), out InitializerValue? initializerValue) ||
            initializerValue.Value is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "log-to-eventhub",
                nameof(LogToEventHubConfig.Value)
            ));
            return;
        }

        element.Add(initializerValue.Value);

        context.AddPolicy(element);
    }
}