// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class HttpDataSourceCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IBackendContext.HttpDataSource);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<HttpDataSourceConfig>(context, "http-data-source", out var values))
        {
            return;
        }

        var element = new XElement("http-data-source");
        var httpRequest = new XElement("http-request");

        if (values.TryGetValue(nameof(HttpDataSourceConfig.Url), out var url))
        {
            httpRequest.Add(new XElement("set-url", url.Value!));
        }

        if (values.TryGetValue(nameof(HttpDataSourceConfig.Method), out var method))
        {
            httpRequest.Add(new XElement("set-method", method.Value!));
        }

        if (values.TryGetValue(nameof(HttpDataSourceConfig.Headers), out var headers))
        {
            BaseSetHeaderCompiler.HandleHeaders(context, httpRequest, headers);
        }

        if (values.TryGetValue(nameof(HttpDataSourceConfig.Body), out var body))
        {
            SetBodyCompiler.HandleBody(context, httpRequest, body);
        }

        if (values.TryGetValue(nameof(HttpDataSourceConfig.Authentication), out var authentication))
        {
            HandleAuthentication(context, httpRequest, authentication);
        }

        element.Add(httpRequest);

        var hasResponseHeaders = values.TryGetValue(nameof(HttpDataSourceConfig.ResponseHeaders), out var responseHeaders);
        var hasResponseBody = values.TryGetValue(nameof(HttpDataSourceConfig.ResponseBody), out var responseBody);

        if (hasResponseHeaders || hasResponseBody)
        {
            var httpResponse = new XElement("http-response");

            if (hasResponseHeaders)
            {
                BaseSetHeaderCompiler.HandleHeaders(context, httpResponse, responseHeaders!);
            }

            if (hasResponseBody)
            {
                SetBodyCompiler.HandleBody(context, httpResponse, responseBody!);
            }

            element.Add(httpResponse);
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
