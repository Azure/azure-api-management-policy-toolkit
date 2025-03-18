// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SendRequestCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SendRequest);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<SendRequestConfig>(context, "send-request", out var values))
        {
            return;
        }

        var element = new XElement("send-request");

        if (!element.AddAttribute(values, nameof(SendRequestConfig.ResponseVariableName), "response-variable-name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "send-request",
                nameof(SendRequestConfig.ResponseVariableName)
            ));
            return;
        }

        element.AddAttribute(values, nameof(SendRequestConfig.Mode), "mode");
        element.AddAttribute(values, nameof(SendRequestConfig.Timeout), "timeout");
        element.AddAttribute(values, nameof(SendRequestConfig.IgnoreError), "ignore-error");

        if (values.TryGetValue(nameof(SendRequestConfig.Url), out var url))
        {
            element.Add(new XElement("set-url", url.Value!));
        }

        if (values.TryGetValue(nameof(SendRequestConfig.Method), out var method))
        {
            element.Add(new XElement("set-method", method.Value!));
        }

        if (values.TryGetValue(nameof(SendRequestConfig.Headers), out var headers))
        {
            SetHeaderCompiler.HandleHeaders(context, element, headers);
        }

        if (values.TryGetValue(nameof(SendRequestConfig.Body), out var body))
        {
            SetBodyCompiler.HandleBody(context, element, body);
        }

        if (values.TryGetValue(nameof(SendRequestConfig.Authentication), out var authentication))
        {
            HandleAuthentication(context, element, authentication);
        }

        if (values.TryGetValue(nameof(SendRequestConfig.Proxy), out var proxy))
        {
            ProxyCompiler.HandleProxy(context, element, proxy);
        }

        context.AddPolicy(element);
    }

    private void HandleAuthentication(ICompilationContext context, XElement element, InitializerValue authentication)
    {
        var values = authentication.NamedValues;
        if (values is null)
        {
            return;
        }

        switch (authentication.Type)
        {
            case nameof(CertificateAuthenticationConfig):
                HandleCertificateAuthentication(context, element, values, authentication.Node);
                break;
            case nameof(BasicAuthenticationConfig):
                HandleBasicAuthentication(context, element, values, authentication.Node);
                break;
            case nameof(ManagedIdentityAuthenticationConfig):
                HandleManagedIdentityAuthentication(context, element, values, authentication.Node);
                break;
            default:
                context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedType,
                    authentication.Node.GetLocation(),
                    $"{element.Name}",
                    authentication.Type
                ));
                break;
        }
    }

    private void HandleBasicAuthentication(
        ICompilationContext context,
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        SyntaxNode node)
    {
        var basicElement = new XElement("authentication-basic");
        if (!basicElement.AddAttribute(values, nameof(BasicAuthenticationConfig.Username), "username"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                $"{element.Name}.authentication-basic",
                nameof(BasicAuthenticationConfig.Username)
            ));
            return;
        }

        if (!basicElement.AddAttribute(values, nameof(BasicAuthenticationConfig.Password), "password"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                $"{element.Name}.authentication-basic",
                nameof(BasicAuthenticationConfig.Password)
            ));
            return;
        }

        element.Add(basicElement);
    }

    private void HandleCertificateAuthentication(
        ICompilationContext context,
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        SyntaxNode node)
    {
        var certElement = new XElement("authentication-certificate");
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

    private void HandleManagedIdentityAuthentication(
        ICompilationContext context,
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        SyntaxNode node)
    {
        var certElement = new XElement("authentication-managed-identity");
        if (!certElement.AddAttribute(values, nameof(ManagedIdentityAuthenticationConfig.Resource), "resource"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                $"{element.Name}.authentication-managed-identity",
                nameof(ManagedIdentityAuthenticationConfig.Resource)
            ));
        }

        certElement.AddAttribute(values, nameof(ManagedIdentityAuthenticationConfig.ClientId), "client-id");
        certElement.AddAttribute(values, nameof(ManagedIdentityAuthenticationConfig.OutputTokenVariableName),
            "output-token-variable-name");
        certElement.AddAttribute(values, nameof(ManagedIdentityAuthenticationConfig.IgnoreError), "ignore-error");
        element.Add(certElement);
    }
}