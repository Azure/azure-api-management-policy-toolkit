// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ProxyCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.Proxy);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ProxyConfig>(context, "proxy",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement? element = HandleProxy(context, node, values);
        if (element is not null)
        {
            context.AddPolicy(element);
        }
    }

    public static void HandleProxy(ICompilationContext context, XElement element, InitializerValue value)
    {
        if (!value.TryGetValues<ProxyConfig>(out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                value.Node.GetLocation(),
                $"{element.Name}.proxy",
                nameof(ProxyConfig)
            ));
            return;
        }

        XElement? proxyElement = HandleProxy(context, value.Node, values);
        if (proxyElement is not null)
        {
            element.Add(proxyElement);
        }
    }

    private static XElement? HandleProxy(ICompilationContext context, SyntaxNode node,
        IReadOnlyDictionary<string, InitializerValue> values)
    {
        XElement element = new("proxy");
        if (!element.AddAttribute(values, nameof(ProxyConfig.Url), "url"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "proxy",
                nameof(ProxyConfig.Url)
            ));
            return null;
        }

        element.AddAttribute(values, nameof(ProxyConfig.Username), "username");
        element.AddAttribute(values, nameof(ProxyConfig.Password), "password");

        return element;
    }
}