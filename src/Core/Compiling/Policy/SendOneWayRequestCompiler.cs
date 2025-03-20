// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SendOneWayRequestCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.SendOneWayRequest);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<SendOneWayRequestConfig>(context, "send-one-way-request",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("send-one-way-request");

        element.AddAttribute(values, nameof(SendOneWayRequestConfig.Mode), "mode");
        element.AddAttribute(values, nameof(SendOneWayRequestConfig.Timeout), "timeout");

        if (values.TryGetValue(nameof(SendOneWayRequestConfig.Url), out InitializerValue? url))
        {
            element.Add(new XElement("set-url", url.Value!));
        }

        if (values.TryGetValue(nameof(SendOneWayRequestConfig.Method), out InitializerValue? method))
        {
            element.Add(new XElement("set-method", method.Value!));
        }

        if (values.TryGetValue(nameof(SendOneWayRequestConfig.Headers), out InitializerValue? headers))
        {
            BaseSetHeaderCompiler.HandleHeaders(context, element, headers);
        }

        if (values.TryGetValue(nameof(SendOneWayRequestConfig.Body), out InitializerValue? body))
        {
            SetBodyCompiler.HandleBody(context, element, body);
        }

        if (values.TryGetValue(nameof(SendOneWayRequestConfig.Authentication), out InitializerValue? authentication))
        {
            HandleAuthentication(context, element, authentication);
        }

        if (values.TryGetValue(nameof(SendOneWayRequestConfig.Proxy), out InitializerValue? proxy))
        {
            ProxyCompiler.HandleProxy(context, element, proxy);
        }

        context.AddPolicy(element);
    }

    private void HandleAuthentication(ICompilationContext context, XElement element, InitializerValue authentication)
    {
        IReadOnlyDictionary<string, InitializerValue>? values = authentication.NamedValues;
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