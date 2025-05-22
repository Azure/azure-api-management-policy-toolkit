// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SendRequestCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SendRequest);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
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
            BaseSetHeaderCompiler.HandleHeaders(context, element, headers);
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

    private void HandleAuthentication(IDocumentCompilationContext context, XElement element,
        InitializerValue authentication)
    {
        var values = authentication.NamedValues;
        if (values is null)
        {
            return;
        }

        switch (authentication.Type)
        {
            case nameof(CertificateAuthenticationConfig):
                AuthenticationCertificateCompiler.HandleCertificateAuthentication(context, element, values,
                    authentication.Node);
                break;
            case nameof(BasicAuthenticationConfig):
                AuthenticationBasicCompiler.HandleBasicAuthentication(context, element, values, authentication.Node);
                break;
            case nameof(ManagedIdentityAuthenticationConfig):
                AuthenticationManagedIdentityCompiler.HandleManagedIdentityAuthentication(context, element, values,
                    authentication.Node);
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
}