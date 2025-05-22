// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class AuthenticationCertificateCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.AuthenticationCertificate);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<CertificateAuthenticationConfig>(
                context,
                "authentication-certificate",
                out var values))
        {
            return;
        }

        var certElement = new XElement("authentication-certificate");
        if (new[]
            {
                certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.Thumbprint), "thumbprint"),
                certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.CertificateId),
                    "certificate-id"),
                certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.Body), "body")
            }.Count(x => x) != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTreeShouldBeDefined,
                node.ArgumentList.GetLocation(),
                "authentication-certificate",
                nameof(CertificateAuthenticationConfig.Thumbprint),
                nameof(CertificateAuthenticationConfig.CertificateId),
                nameof(CertificateAuthenticationConfig.Body)
            ));
            return;
        }

        certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.Password), "password");
        context.AddPolicy(certElement);
    }

    public static void HandleCertificateAuthentication(
        ICompilationContext context,
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        SyntaxNode node)
    {
        XElement certElement = new("authentication-certificate");
        certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.Password), "password");

        if (new[]
            {
                certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.Thumbprint), "thumbprint"),
                certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.CertificateId),
                    "certificate-id"),
                certElement.AddAttribute(values, nameof(CertificateAuthenticationConfig.Body), "body")
            }.Count(b => b) != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTreeShouldBeDefined,
                node.GetLocation(),
                $"{element.Name}.authentication-certificate",
                nameof(CertificateAuthenticationConfig.Thumbprint),
                nameof(CertificateAuthenticationConfig.CertificateId),
                nameof(CertificateAuthenticationConfig.Body)
            ));
            return;
        }

        element.Add(certElement);
    }
}