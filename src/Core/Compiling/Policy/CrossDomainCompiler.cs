// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CrossDomainCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.CrossDomain);

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

        ExpressionSyntax expression = node.ArgumentList.Arguments[0].Expression;

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
            XElement document = XElement.Parse(literal.Token.ValueText);
            context.AddPolicy(new XElement("cross-domain", document));
        }
        catch (XmlException ex)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterHasXmlErrors,
                literal.GetLocation(),
                "cross-domain",
                "policy",
                ex.ToString()
            ));
        }
    }
}