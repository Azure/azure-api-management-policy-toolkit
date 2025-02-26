// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class RetryCompiler : IMethodPolicyHandler
{
    private readonly Lazy<BlockCompiler> _blockCompiler;

    public RetryCompiler(Lazy<BlockCompiler> blockCompiler)
    {
        _blockCompiler = blockCompiler;
    }

    public string MethodName { get; } = nameof(IInboundContext.Retry);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 2)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "retry"));
            return;
        }

        ExpressionSyntax configExpression = node.ArgumentList.Arguments[0].Expression;
        if (!configExpression.TryExtractingConfig<RetryConfig>(context, "retry",
                out IReadOnlyDictionary<string, InitializerValue>? config))
        {
            return;
        }

        ExpressionSyntax childPoliciesLambdaExpression = node.ArgumentList.Arguments[1].Expression;
        if (childPoliciesLambdaExpression is not LambdaExpressionSyntax lambda)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ValueShouldBe,
                childPoliciesLambdaExpression.GetLocation(),
                "retry",
                nameof(LambdaExpressionSyntax)));
            return;
        }

        if (lambda.Block is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.NotSupportedStatement,
                lambda.GetLocation(),
                childPoliciesLambdaExpression.GetType().FullName
            ));
            return;
        }

        XElement element = new("retry");
        if (!element.AddAttribute(config, nameof(RetryConfig.Condition), "condition"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "retry",
                nameof(RetryConfig.Condition)
            ));
            return;
        }

        if (!element.AddAttribute(config, nameof(RetryConfig.Count), "count"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "retry",
                nameof(RetryConfig.Count)
            ));
            return;
        }

        if (!element.AddAttribute(config, nameof(RetryConfig.Interval), "interval"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "retry",
                nameof(RetryConfig.Interval)
            ));
            return;
        }

        element.AddAttribute(config, nameof(RetryConfig.MaxInterval), "max-interval");
        element.AddAttribute(config, nameof(RetryConfig.Delta), "delta");
        element.AddAttribute(config, nameof(RetryConfig.FirstFastRetry), "first-fast-retry");

        SubCompilationContext subContext = new(context, element);
        _blockCompiler.Value.Compile(subContext, lambda.Block);

        context.AddPolicy(element);
    }
}