// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SetBodyCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SetBody);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count is > 2 or 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "set-body"));
            return;
        }

        var value = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        var element = new XElement("set-body", value);
        if (node.ArgumentList.Arguments.Count == 2)
        {
            var contentType = node.ArgumentList.Arguments[1].Expression.ProcessExpression(context);
            if (contentType is { Type: nameof(SetBodyConfig), NamedValues: not null })
            {
                if (contentType.NamedValues.TryGetValue(nameof(SetBodyConfig.Template), out var template))
                {
                    if (template.Value != "liquid")
                    {
                        context.Report(Diagnostic.Create(
                            CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                            template.Node.GetLocation(),
                            "forward-request.template",
                            "liquid"
                        ));
                    }
                    else
                    {
                        element.Add(new XAttribute("template", template.Value));
                    }
                }

                if (contentType.NamedValues.TryGetValue(nameof(SetBodyConfig.XsiNil), out var xsiNil))
                {
                    if (xsiNil.Value != "blank" && xsiNil.Value != "null")
                    {
                        context.Report(Diagnostic.Create(
                            CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                            xsiNil.Node.GetLocation(),
                            "forward-request.xsi-nil",
                            "blank",
                            "null"
                        ));
                    }
                    else
                    {
                        element.Add(new XAttribute("xsi-nil", xsiNil.Value));
                    }
                }

                if (contentType.NamedValues.TryGetValue(nameof(SetBodyConfig.ParseDate), out var parseDate))
                {
                    element.Add(new XAttribute("parse-date", parseDate.Value!));
                }
            }
        }

        context.AddPolicy(element);
    }

    public static void HandleBody(IDocumentCompilationContext context, XElement element, InitializerValue body)
    {
        if (!body.TryGetValues<BodyConfig>(out var config))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                body.Node.GetLocation(),
                $"{element.Name}.set-body",
                nameof(BodyConfig)
            ));
            return;
        }

        if (!config.TryGetValue(nameof(BodyConfig.Content), out var content))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                body.Node.GetLocation(),
                $"{element.Name}.set-body",
                nameof(BodyConfig.Content)
            ));
            return;
        }

        var bodyElement = new XElement("set-body", content.Value!);
        bodyElement.AddAttribute(config, nameof(BodyConfig.Template), "template");
        bodyElement.AddAttribute(config, nameof(BodyConfig.XsiNil), "xsi-nil");
        bodyElement.AddAttribute(config, nameof(BodyConfig.ParseDate), "parse-date");
        element.Add(bodyElement);
    }
}