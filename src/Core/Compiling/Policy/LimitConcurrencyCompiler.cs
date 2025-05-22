// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class LimitConcurrencyCompiler : IMethodPolicyHandler
{
    private readonly Lazy<BlockCompiler> _blockCompiler;

    public LimitConcurrencyCompiler(Lazy<BlockCompiler> blockCompiler)
    {
        _blockCompiler = blockCompiler;
    }

    public string MethodName => nameof(IInboundContext.LimitConcurrency);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 2)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "limit-concurrency"));
            return;
        }

        ExpressionSyntax configExpression = node.ArgumentList.Arguments[0].Expression;
        if (!configExpression.TryExtractingConfig<LimitConcurrencyConfig>(context, "limit-concurrency",
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
                "limit-concurrency",
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

        XElement element = new("limit-concurrency");

        if (!element.AddAttribute(config, nameof(LimitConcurrencyConfig.Key), "key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "limit-concurrency",
                nameof(LimitConcurrencyConfig.Key)
            ));
            return;
        }

        if (!element.AddAttribute(config, nameof(LimitConcurrencyConfig.MaxCount), "max-count"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "limit-concurrency",
                nameof(LimitConcurrencyConfig.MaxCount)
            ));
            return;
        }

        SubDocumentCompilationContext subContext = new(context, element);
        _blockCompiler.Value.Compile(subContext, lambda.Block);

        context.AddPolicy(element);
    }
}