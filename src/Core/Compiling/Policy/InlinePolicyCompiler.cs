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

    private static XElement CreateRazorFromString(LiteralExpressionSyntax literal)
    {
        var cleanXml = RazorCodeFormatter.ToCleanXml(literal.Token.ValueText, out var markerToCode);
        var xml = XElement.Parse(cleanXml);

        foreach (var attribute in xml.DescendantsAndSelf().Attributes())
        {
            attribute.Value = RestoreExpressions(attribute.Value, markerToCode);
        }

        // Every text node is restored, so text next to child elements doesn't keep its marker.
        foreach (var node in xml.DescendantNodes())
        {
            if (node is XText textNode)
            {
                textNode.Value = RestoreExpressions(textNode.Value, markerToCode);
            }
            else if (node is XComment comment)
            {
                comment.Value = RestoreExpressions(comment.Value, markerToCode);

                // The marker hid it from the parser, but a comment can't hold this and can't be written.
                if (comment.Value.Contains("--", StringComparison.Ordinal))
                {
                    throw new XmlException($"The comment '{comment.Value}' holds an expression that isn't valid in a comment.");
                }
            }
            else if (node is XProcessingInstruction instruction)
            {
                instruction.Data = RestoreExpressions(instruction.Data, markerToCode);

                // as for a comment: the marker hid it from the parser
                if (instruction.Data.Contains("?>", StringComparison.Ordinal))
                {
                    throw new XmlException($"The processing instruction '{instruction.Data}' holds an expression that isn't valid in one.");
                }
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