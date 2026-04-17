// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateGraphqlRequestCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateGraphqlRequest);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateGraphqlRequestConfig>(context, "validate-graphql-request",
                out var values))
        {
            return;
        }

        XElement element = new("validate-graphql-request");

        element.AddAttribute(values, nameof(ValidateGraphqlRequestConfig.ErrorVariableName), "error-variable-name");
        element.AddAttribute(values, nameof(ValidateGraphqlRequestConfig.MaxDepth), "max-depth");
        element.AddAttribute(values, nameof(ValidateGraphqlRequestConfig.MaxSize), "max-size");
        element.AddAttribute(values, nameof(ValidateGraphqlRequestConfig.MaxTotalDepth), "max-total-depth");
        element.AddAttribute(values, nameof(ValidateGraphqlRequestConfig.MaxComplexity), "max-complexity");

        if (values.TryGetValue(nameof(ValidateGraphqlRequestConfig.Authorize), out var authorizeValue))
        {
            HandleAuthorize(context, authorizeValue, element);
        }

        context.AddPolicy(element);
    }

    private static void HandleAuthorize(IDocumentCompilationContext context, InitializerValue authorizeValue,
        XElement parentElement)
    {
        if (!authorizeValue.TryGetValues<AuthorizeConfig>(out var authorizeValues))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                authorizeValue.Node.GetLocation(),
                "validate-graphql-request.authorize",
                nameof(AuthorizeConfig)
            ));
            return;
        }

        XElement authorizeElement = new("authorize");

        if (authorizeValues.TryGetValue(nameof(AuthorizeConfig.Rules), out var rulesValue))
        {
            foreach (var ruleValue in rulesValue.UnnamedValues ?? [])
            {
                if (!ruleValue.TryGetValues<AuthorizeRuleConfig>(out var ruleValues))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                        ruleValue.Node.GetLocation(),
                        "validate-graphql-request.authorize.rule",
                        nameof(AuthorizeRuleConfig)
                    ));
                    continue;
                }

                XElement ruleElement = new("rule");
                ruleElement.AddAttribute(ruleValues, nameof(AuthorizeRuleConfig.Path), "path");
                ruleElement.AddAttribute(ruleValues, nameof(AuthorizeRuleConfig.Action), "action");
                authorizeElement.Add(ruleElement);
            }
        }

        parentElement.Add(authorizeElement);
    }
}
