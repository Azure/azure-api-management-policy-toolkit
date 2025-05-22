// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CorsCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.Cors);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<CorsConfig>(context, "cors", out var values))
        {
            return;
        }

        var element = new XElement("cors");

        element.AddAttribute(values, nameof(CorsConfig.AllowCredentials), "allow-credentials");
        element.AddAttribute(values, nameof(CorsConfig.TerminateUnmatchedRequest), "terminate-unmatched-request");

        if (!values.TryGetValue(nameof(CorsConfig.AllowedOrigins), out var allowedOrigins))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cors",
                nameof(CorsConfig.AllowedOrigins)
            ));
            return;
        }

        var origins = (allowedOrigins.UnnamedValues ?? [])
            .Select(origin => new XElement("origin", origin.Value!))
            .ToArray<object>();
        if (origins.Length == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                allowedOrigins.Node.GetLocation(),
                "cors",
                nameof(CorsConfig.AllowedOrigins)
            ));
            return;
        }

        element.Add(new XElement("allowed-origins", origins));

        if (!values.TryGetValue(nameof(CorsConfig.AllowedHeaders), out var allowedHeaders))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cors",
                nameof(CorsConfig.AllowedHeaders)
            ));
            return;
        }

        var headers = (allowedHeaders.UnnamedValues ?? [])
            .Select(origin => new XElement("header", origin.Value!))
            .ToArray<object>();
        if (headers.Length == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                allowedHeaders.Node.GetLocation(),
                "cors",
                nameof(CorsConfig.AllowedHeaders)
            ));
            return;
        }

        element.Add(new XElement("allowed-headers", headers));

        if (values.TryGetValue(nameof(CorsConfig.AllowedMethods), out var allowedMethods))
        {
            var allowedMethodsElement = new XElement("allowed-methods");
            allowedMethodsElement.AddAttribute(values, nameof(CorsConfig.PreflightResultMaxAge),
                "preflight-result-max-age");
            var methods = (allowedMethods.UnnamedValues ?? [])
                .Select(m => new XElement("method", m.Value!))
                .ToArray<object>();
            if (methods.Length == 0)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterIsEmpty,
                    allowedMethods.Node.GetLocation(),
                    "cors",
                    nameof(CorsConfig.AllowedMethods)
                ));
            }

            allowedMethodsElement.Add(methods);
            element.Add(allowedMethodsElement);
        }

        if (values.TryGetValue(nameof(CorsConfig.ExposeHeaders), out var exposeHeaders))
        {
            var exposeHeadersElements = (exposeHeaders.UnnamedValues ?? [])
                .Select(h => new XElement("header", h.Value!))
                .ToArray<object>();
            if (exposeHeadersElements.Length == 0)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterIsEmpty,
                    exposeHeaders.Node.GetLocation(),
                    "cors",
                    nameof(CorsConfig.ExposeHeaders)
                ));
            }

            element.Add(new XElement("expose-headers", exposeHeadersElements));
        }

        context.AddPolicy(element);
    }
}