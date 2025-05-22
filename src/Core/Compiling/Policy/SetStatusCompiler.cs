// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SetStatusCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SetStatus);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<StatusConfig>(context, "set-status", out var values))
        {
            return;
        }

        var statusElement = new XElement("set-status");

        if (!statusElement.AddAttribute(values, nameof(StatusConfig.Code), "code"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "set-status",
                nameof(StatusConfig.Code)
            ));
            return;
        }

        statusElement.AddAttribute(values, nameof(StatusConfig.Reason), "reason");

        context.AddPolicy(statusElement);
    }

    public static void HandleStatus(ICompilationContext context, XElement element, InitializerValue status)
    {
        if (!status.TryGetValues<StatusConfig>(out var config))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                status.Node.GetLocation(),
                $"{element.Name}.set-status",
                nameof(StatusConfig)
            ));
            return;
        }

        var statusElement = new XElement("set-status");

        if (!statusElement.AddAttribute(config, nameof(StatusConfig.Code), "code"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                status.Node.GetLocation(),
                $"{element.Name}.set-status",
                nameof(StatusConfig.Code)
            ));
            return;
        }

        statusElement.AddAttribute(config, nameof(StatusConfig.Reason), "reason");
        element.Add(statusElement);
    }
}