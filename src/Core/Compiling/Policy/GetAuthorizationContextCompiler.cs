// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class GetAuthorizationContextCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.GetAuthorizationContext);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<GetAuthorizationContextConfig>(
                context,
                "get-authorization-context",
                out var values))
        {
            return;
        }

        var element = new XElement("get-authorization-context");

        if (!element.AddAttribute(values, nameof(GetAuthorizationContextConfig.ProviderId), "provider-id"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "get-authorization-context",
                nameof(GetAuthorizationContextConfig.ProviderId)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(GetAuthorizationContextConfig.AuthorizationId), "authorization-id"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "get-authorization-context",
                nameof(GetAuthorizationContextConfig.AuthorizationId)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(GetAuthorizationContextConfig.ContextVariableName), "context-variable-name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "get-authorization-context",
                nameof(GetAuthorizationContextConfig.ContextVariableName)
            ));
            return;
        }

        element.AddAttribute(values, nameof(GetAuthorizationContextConfig.IdentityType), "identity-type");
        element.AddAttribute(values, nameof(GetAuthorizationContextConfig.Identity), "identity");
        element.AddAttribute(values, nameof(GetAuthorizationContextConfig.IgnoreError), "ignore-error");

        context.AddPolicy(element);
    }
}
