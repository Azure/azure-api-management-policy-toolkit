// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class XmlToJsonCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.XmlToJson);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<XmlToJsonConfig>(context, "xml-to=json", out var values))
        {
            return;
        }

        var element = new XElement("xml-to-json");

        if (!element.AddAttribute(values, nameof(XmlToJsonConfig.Kind), "kind"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "xml-to-json",
                nameof(XmlToJsonConfig.Kind)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(XmlToJsonConfig.Apply), "apply"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "xml-to-json",
                nameof(XmlToJsonConfig.Apply)
            ));
            return;
        }

        element.AddAttribute(values, nameof(XmlToJsonConfig.ConsiderAcceptHeader), "consider-accept-header");
        element.AddAttribute(values, nameof(XmlToJsonConfig.AlwaysArrayChildElements), "always-array-child-elements");

        context.AddPolicy(element);
    }
}