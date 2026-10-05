// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ForwardRequestCompiler : IMethodPolicyHandler
{
    readonly static IReadOnlyDictionary<string, string> FieldToAttribute = new Dictionary<string, string>
    {
        { nameof(ForwardRequestConfig.Timeout), "timeout" },
        { nameof(ForwardRequestConfig.TimeoutMs), "timeout-ms" },
        { nameof(ForwardRequestConfig.ContinueTimeout), "continue-timeout" },
        { nameof(ForwardRequestConfig.HttpVersion), "http-version" },
        { nameof(ForwardRequestConfig.FollowRedirects), "follow-redirects" },
        { nameof(ForwardRequestConfig.BufferRequestBody), "buffer-request-body" },
        { nameof(ForwardRequestConfig.BufferResponse), "buffer-response" },
        { nameof(ForwardRequestConfig.FailOnErrorStatusCode), "fail-on-error-status-code" }
    };

    public string MethodName => nameof(IBackendContext.ForwardRequest);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count > 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "forward-request"
            ));
            return;
        }

        var element = new XElement("forward-request");
        if (node.ArgumentList.Arguments.Count == 1)
        {
            var argument = node.ArgumentList.Arguments[0].Expression;
            if (!argument.TryExtractingConfig<ForwardRequestConfig>(context, "forward-request", out var values))
            {
                return;
            }

            if (values.ContainsKey(nameof(ForwardRequestConfig.Timeout))
                && values.ContainsKey(nameof(ForwardRequestConfig.TimeoutMs)))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                    argument.GetLocation(),
                    "forward-request",
                    nameof(ForwardRequestConfig.Timeout),
                    nameof(ForwardRequestConfig.TimeoutMs)
                ));
            }

            foreach ((string key, InitializerValue value) in values)
            {
                var name = FieldToAttribute.GetValueOrDefault(key, key);
                element.Add(new XAttribute(name, value.Value!));
            }
        }

        context.AddPolicy(element);
    }
}