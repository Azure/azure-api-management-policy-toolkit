// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ReturnResponseCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ReturnResponse);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ReturnResponseConfig>(context, "return-response", out var values))
        {
            return;
        }

        var element = new XElement("return-response");

        element.AddAttribute(values, nameof(ReturnResponseConfig.ResponseVariableName), "response-variable-name");

        if (values.TryGetValue(nameof(ReturnResponseConfig.Status), out var statusConfig))
        {
            SetStatusCompiler.HandleStatus(context, element, statusConfig);
        }

        if (values.TryGetValue(nameof(ReturnResponseConfig.Headers), out var headers))
        {
            BaseSetHeaderCompiler.HandleHeaders(context, element, headers);
        }

        if (values.TryGetValue(nameof(ReturnResponseConfig.Body), out var body))
        {
            SetBodyCompiler.HandleBody(context, element, body);
        }

        context.AddPolicy(element);
    }
}