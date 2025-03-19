// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class AuthenticationBasicCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.AuthenticationBasic);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 2)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "authentication-basic"));
            return;
        }

        var username = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        var password = node.ArgumentList.Arguments[1].Expression.ProcessParameter(context);
        context.AddPolicy(new XElement("authentication-basic", new XAttribute("username", username),
            new XAttribute("password", password)));
    }

    public static void HandleBasicAuthentication(
        ICompilationContext context,
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        SyntaxNode node)
    {
        XElement basicElement = new("authentication-basic");
        if (!basicElement.AddAttribute(values, nameof(BasicAuthenticationConfig.Username), "username"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "authentication-basic",
                nameof(BasicAuthenticationConfig.Username)
            ));
            return;
        }

        if (!basicElement.AddAttribute(values, nameof(BasicAuthenticationConfig.Password), "password"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "authentication-basic",
                nameof(BasicAuthenticationConfig.Password)
            ));
            return;
        }

        element.Add(basicElement);
    }
}