// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CheckHeaderCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.CheckHeader);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<CheckHeaderConfig>(context, "check-header", out var values))
        {
            return;
        }

        var element = new XElement("check-header");

        if (!element.AddAttribute(values, nameof(CheckHeaderConfig.Name), "name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "check-header",
                nameof(CheckHeaderConfig.Name)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CheckHeaderConfig.FailCheckHttpCode), "failed-check-httpcode"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "check-header",
                nameof(CheckHeaderConfig.FailCheckHttpCode)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CheckHeaderConfig.FailCheckErrorMessage),
                "failed-check-error-message"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "check-header",
                nameof(CheckHeaderConfig.FailCheckErrorMessage)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CheckHeaderConfig.IgnoreCase), "ignore-case"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "check-header",
                nameof(CheckHeaderConfig.IgnoreCase)
            ));
            return;
        }

        if (!values.TryGetValue(nameof(CheckHeaderConfig.Values), out var headerValues))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "check-header",
                nameof(CheckHeaderConfig.Values)
            ));
            return;
        }

        var elements = (headerValues.UnnamedValues ?? [])
            .Select(origin => new XElement("value", origin.Value!))
            .ToArray<object>();
        if (elements.Length == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                headerValues.Node.GetLocation(),
                "check-header",
                nameof(CheckHeaderConfig.Values)
            ));
            return;
        }

        element.Add(elements);

        context.AddPolicy(element);
    }
}