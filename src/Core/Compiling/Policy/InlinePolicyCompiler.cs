// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class InlinePolicyCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.InlinePolicy);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                MethodName
            ));
            return;
        }

        var expression = node.ArgumentList.Arguments[0].Expression;

        if (expression is not LiteralExpressionSyntax literal)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                expression.GetLocation(),
                MethodName,
                "string literal"
            ));
            return;
        }

        try
        {
            XElement xml = CreateRazorFromString(literal);
            context.AddPolicy(xml);
        }
        catch (XmlException ex)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterHasXmlErrors,
                literal.GetLocation(),
                "InlinePolicy",
                "policy",
                ex.ToString()
            ));
        }
    }

    private static XElement CreateRazorFromString(LiteralExpressionSyntax literal)
    {
        var cleanXml = RazorCodeFormatter.ToCleanXml(literal.Token.ValueText, out var markerToCode);
        var xml = XElement.Parse(cleanXml);

        foreach (XElement element in xml.DescendantsAndSelf())
        {
            if (element.HasAttributes)
            {
                foreach (var a in element.Attributes())
                {
                    if (markerToCode.TryGetValue(a.Value, out var attributeCode))
                    {
                        a.Value = attributeCode;
                    }
                }
            }

            if (markerToCode.TryGetValue(element.Value, out var valueCode))
            {
                element.Value = valueCode;
            }
        }

        return xml;
    }
}