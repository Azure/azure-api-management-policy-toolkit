// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class MockResponseCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.MockResponse);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count > 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "mock-response"));
            return;
        }

        var element = new XElement("mock-response");
        if (arguments.Count == 1)
        {
            HandleConfig(context, element, arguments[0].Expression.ProcessExpression(context));
        }

        context.AddPolicy(element);
    }

    private void HandleConfig(IDocumentCompilationContext context, XElement element, InitializerValue value)
    {
        if (value.Type != nameof(MockResponseConfig))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                value.Node.GetLocation(),
                "mock-response",
                nameof(AddressRange)
            ));
            return;
        }

        var values = value.NamedValues;
        if (values is null)
        {
            return;
        }

        element.AddAttribute(values, nameof(MockResponseConfig.StatusCode), "status-code");
        element.AddAttribute(values, nameof(MockResponseConfig.ContentType), "content-type");
        element.AddAttribute(values, nameof(MockResponseConfig.Index), "index");
    }
}