// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class JsonToXmlCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.JsonToXml);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<JsonToXmlConfig>(context, "json-to-xml", out var values))
        {
            return;
        }

        var element = new XElement("json-to-xml");
        if (!element.AddAttribute(values, nameof(JsonToXmlConfig.Apply), "apply"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "json-to-xml",
                nameof(JsonToXmlConfig.Apply)
            ));
            return;
        }

        element.AddAttribute(values, nameof(JsonToXmlConfig.ConsiderAcceptHeader), "consider-accept-header");
        element.AddAttribute(values, nameof(JsonToXmlConfig.ParseDate), "parse-date");
        element.AddAttribute(values, nameof(JsonToXmlConfig.NamespaceSeparator), "namespace-separator");
        element.AddAttribute(values, nameof(JsonToXmlConfig.NamespacePrefix), "namespace-prefix");
        element.AddAttribute(values, nameof(JsonToXmlConfig.AttributeBlockName), "attribute-block-name");

        context.AddPolicy(element);
    }
}