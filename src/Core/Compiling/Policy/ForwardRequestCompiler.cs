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

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
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
            if (node.ArgumentList.Arguments[0].Expression is not ObjectCreationExpressionSyntax config)
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotAnObjectCreation,
                    node.ArgumentList.Arguments[0].Expression.GetLocation(),
                    "forward-request"));
                return;
            }

            var initializer = config.Process(context);
            if (initializer.Type != nameof(ForwardRequestConfig))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    config.GetLocation(),
                    "forward-request",
                    nameof(ForwardRequestConfig)
                ));
                return;
            }

            if (initializer.NamedValues is not null)
            {
                if (initializer.NamedValues.ContainsKey(nameof(ForwardRequestConfig.Timeout))
                    && initializer.NamedValues.ContainsKey(nameof(ForwardRequestConfig.TimeoutMs)))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.OnlyOneOfTwoShouldBeDefined,
                        config.GetLocation(),
                        "forward-request",
                        nameof(ForwardRequestConfig.Timeout),
                        nameof(ForwardRequestConfig.TimeoutMs)
                    ));
                }

                foreach ((string key, InitializerValue value) in initializer.NamedValues)
                {
                    var name = FieldToAttribute.GetValueOrDefault(key, key);
                    element.Add(new XAttribute(name, value.Value!));
                }
            }
        }

        context.AddPolicy(element);
    }
}