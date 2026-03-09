// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class InvokeRequestCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.InvokeRequest);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<InvokeRequestConfig>(context, "invoke-request", out var values))
        {
            return;
        }

        var element = new XElement("invoke-request");
        element.AddAttribute(values, nameof(InvokeRequestConfig.Method), "method");
        element.AddAttribute(values, nameof(InvokeRequestConfig.Url), "url");
        element.AddAttribute(values, nameof(InvokeRequestConfig.BackendId), "backend-id");
        element.AddAttribute(values, nameof(InvokeRequestConfig.ResponseVariableName), "response-variable-name");

        if (values.TryGetValue(nameof(InvokeRequestConfig.Headers), out var headers))
        {
            HandleHeaders(context, element, headers);
        }

        if (values.TryGetValue(nameof(InvokeRequestConfig.Body), out var body))
        {
            HandleBody(context, element, body);
        }

        context.AddPolicy(element);
    }

    private static void HandleHeaders(IDocumentCompilationContext context, XElement root, InitializerValue headers)
    {
        foreach (var header in headers.UnnamedValues!)
        {
            if (!header.TryGetValues<HeaderConfig>(out var headerValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    header.Node.GetLocation(),
                    $"{root.Name}.header",
                    nameof(HeaderConfig)
                ));
                continue;
            }

            if (!headerValues.TryGetValue(nameof(HeaderConfig.Name), out var name))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    header.Node.GetLocation(),
                    $"{root.Name}.header",
                    nameof(HeaderConfig.Name)
                ));
                continue;
            }

            if (headerValues.TryGetValue(nameof(HeaderConfig.Values), out var values) &&
                values.UnnamedValues is not null)
            {
                foreach (var value in values.UnnamedValues)
                {
                    var headerElement = new XElement("header", new XAttribute("name", name.Value!));
                    if (!string.IsNullOrEmpty(value.Value))
                    {
                        headerElement.Add(new XAttribute("value", value.Value!));
                    }

                    root.Add(headerElement);
                }
            }
            else
            {
                root.Add(new XElement("header", new XAttribute("name", name.Value!)));
            }
        }
    }

    private static void HandleBody(IDocumentCompilationContext context, XElement root, InitializerValue body)
    {
        if (!body.TryGetValues<BodyConfig>(out var bodyValues))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                body.Node.GetLocation(),
                $"{root.Name}.body",
                nameof(BodyConfig)
            ));
            return;
        }

        if (!bodyValues.TryGetValue(nameof(BodyConfig.Content), out var content) || content.Value is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                body.Node.GetLocation(),
                $"{root.Name}.body",
                nameof(BodyConfig.Content)
            ));
            return;
        }

        root.Add(new XElement("body", content.Value));
    }
}
