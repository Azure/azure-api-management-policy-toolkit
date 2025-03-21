// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateHeadersCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IOutboundContext.ValidateHeaders);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateHeadersConfig>(context, "validate-headers", out var values))
        {
            return;
        }

        XElement element = new("validate-headers");

        if (!element.AddAttribute(values, nameof(ValidateHeadersConfig.SpecifiedHeaderAction),
                "specified-header-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-headers",
                nameof(ValidateHeadersConfig.SpecifiedHeaderAction)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(ValidateHeadersConfig.UnspecifiedHeaderAction),
                "unspecified-header-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-headers",
                nameof(ValidateHeadersConfig.UnspecifiedHeaderAction)
            ));
            return;
        }

        element.AddAttribute(values, nameof(ValidateHeadersConfig.ErrorsVariableName), "errors-variable-name");

        if (values.TryGetValue(nameof(ValidateHeadersConfig.Headers), out var headerValues))
        {
            HandleHeaders(context, headerValues, element);
        }

        context.AddPolicy(element);
    }

    private static void HandleHeaders(ICompilationContext context, InitializerValue headerValues, XElement element)
    {
        foreach (var headerValue in headerValues.UnnamedValues ?? [])
        {
            if (!headerValue.TryGetValues<ValidateHeader>(out var validateHeaderValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    headerValue.Node.GetLocation(),
                    "validate-headers.header",
                    nameof(ValidateHeader)
                ));
                continue;
            }

            XElement header = new("header");
            if (!header.AddAttribute(validateHeaderValues, nameof(ValidateHeader.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    headerValue.Node.GetLocation(),
                    "validate-headers.header",
                    nameof(ValidateHeader.Name)
                ));
                continue;
            }

            if (!header.AddAttribute(validateHeaderValues, nameof(ValidateHeader.Action), "action"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    headerValue.Node.GetLocation(),
                    "validate-headers.header",
                    nameof(ValidateHeader.Action)
                ));
                continue;
            }

            element.Add(header);
        }
    }
}