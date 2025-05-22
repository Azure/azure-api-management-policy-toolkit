// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class XslTransformCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.XslTransform);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<XslTransformConfig>(context, "xsl-transform", out var values))
        {
            return;
        }

        var element = new XElement("xsl-transform");

        if (!values.TryGetValue(nameof(XslTransformConfig.StyleSheet), out var styleSheetValue))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "xsl-transform",
                nameof(XslTransformConfig.StyleSheet)
            ));
            return;
        }

        if (values.TryGetValue(nameof(XslTransformConfig.Parameters), out var parametersValue))
        {
            HandleParameters(context, parametersValue, element);
        }

        try
        {
            var xml = XElement.Parse(styleSheetValue.Value!);
            element.Add(xml);
        }
        catch (XmlException ex)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterHasXmlErrors,
                styleSheetValue.Node.GetLocation(),
                "xsl-transform",
                nameof(XslTransformConfig.StyleSheet),
                ex.ToString()
            ));
        }

        context.AddPolicy(element);
    }

    private static void HandleParameters(ICompilationContext context, InitializerValue parametersValue,
        XElement element)
    {
        foreach (var paramValue in parametersValue.UnnamedValues ?? [])
        {
            if (!paramValue.TryGetValues<XslTransformParameter>(out var paramValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    paramValue.Node.GetLocation(),
                    "xsl-transform.parameter",
                    nameof(XslTransformParameter)
                ));
                continue;
            }

            XElement paramElement = new("parameter");

            if (!paramElement.AddAttribute(paramValues, nameof(XslTransformParameter.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    paramValue.Node.GetLocation(),
                    "xsl-transform.parameter",
                    nameof(XslTransformParameter.Name)
                ));
                continue;
            }

            if (!paramValues.TryGetValue(nameof(XslTransformParameter.Value), out var value))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    paramValue.Node.GetLocation(),
                    "xsl-transform.parameter",
                    nameof(XslTransformConfig.StyleSheet)
                ));
                continue;
            }

            paramElement.Value = value.Value!;
            element.Add(paramElement);
        }
    }
}