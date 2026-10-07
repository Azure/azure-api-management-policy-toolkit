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

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
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

    // The policy is read as API Management's rawxml format, whether or not it happens to be well-formed XML:
    // an expression is C# as it is written, so characters reserved in XML are not escaped in it and an
    // entity such as &amp; in it is not unescaped.
    private static XElement CreateRazorFromString(LiteralExpressionSyntax literal) =>
        CreateFromRawXml(literal.Token.ValueText);

    // Razor-like rawxml: expressions are written unescaped, so they are replaced with markers
    // for the XML reader and put back afterwards.
    private static XElement CreateFromRawXml(string text)
    {
        var cleanXml = RazorCodeFormatter.ToCleanXml(text, out var markerToCode);
        var xml = XElement.Parse(cleanXml);

        foreach (var attribute in xml.DescendantsAndSelf().Attributes())
        {
            attribute.Value = RestoreExpressions(attribute.Value, markerToCode);
        }

        foreach (var node in xml.DescendantNodes())
        {
            if (node is XText textNode)
            {
                textNode.Value = RestoreExpressions(textNode.Value, markerToCode);
            }
            else if (node is XComment comment)
            {
                comment.Value = RestoreExpressions(comment.Value, markerToCode);
            }
        }

        return xml;
    }

    // A value is not always exactly one expression, so every marker in it is replaced.
    private static string RestoreExpressions(string value, IReadOnlyDictionary<string, string> markerToCode)
    {
        if (!value.Contains("__expression__", StringComparison.Ordinal))
        {
            return value;
        }

        foreach (var (marker, code) in markerToCode)
        {
            value = value.Replace(marker, code, StringComparison.Ordinal);
        }

        return value;
    }
}
