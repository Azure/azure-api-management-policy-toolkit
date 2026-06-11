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
        bool useValueElement = false;
        var element = new XElement("set-body");
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

                if (contentType.NamedValues.TryGetValue(nameof(SetBodyConfig.UseValueElement), out var useVal) &&
                    useVal.Value == "true")
                {
                    useValueElement = true;
                }
            }
        }

        if (useValueElement)
            element.Add(new XElement("value", value));
        else
            AddBodyContent(element, value);

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

        var useValueElement = config.TryGetValue(nameof(BodyConfig.UseValueElement), out var useVal) &&
                              useVal.Value == "true";

        var bodyElement = new XElement("set-body");
        bodyElement.AddAttribute(config, nameof(BodyConfig.Template), "template");
        bodyElement.AddAttribute(config, nameof(BodyConfig.XsiNil), "xsi-nil");
        bodyElement.AddAttribute(config, nameof(BodyConfig.ParseDate), "parse-date");

        if (useValueElement)
            bodyElement.Add(new XElement("value", content.Value!));
        else
            AddBodyContent(bodyElement, content.Value!);
        element.Add(bodyElement);
    }

    /// <summary>
    /// Adds body content to a set-body element. When the content is raw XML (starts with '&lt;'),
    /// it is parsed and added as XML nodes so that markup is not escaped. Otherwise it is added
    /// as a plain text node (the existing behaviour for expressions and plain strings).
    /// </summary>
    private static void AddBodyContent(XElement element, string content)
    {
        var trimmed = content?.TrimStart();
        if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("<"))
        {
            try
            {
                // Wrap in a root element so multiple top-level nodes and namespace-prefixed
                // elements are parsed correctly. Use PreserveWhitespace so that any formatting
                // whitespace in the body (e.g. newlines between <Year>/<Month>/<Day>) is kept as
                // text nodes and written verbatim by CustomXmlWriter.WriteNodes.
                var doc = XDocument.Parse("<__root__>" + content + "</__root__>", LoadOptions.PreserveWhitespace);
                foreach (var node in doc.Root!.Nodes())
                    element.Add(node);
                return;
            }
            catch
            {
                // Fall through to plain-text path if the XML is not parseable.
            }
        }
        element.Add(content!);
    }
}