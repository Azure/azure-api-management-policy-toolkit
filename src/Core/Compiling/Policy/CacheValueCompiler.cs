// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CacheValueCompiler : IMethodPolicyHandler
{
    private readonly Lazy<BlockCompiler> _blockCompiler;

    public CacheValueCompiler(Lazy<BlockCompiler> blockCompiler)
    {
        _blockCompiler = blockCompiler;
    }

    public string MethodName => nameof(IInboundContext.CacheValue);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 2)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "cache-value"));
            return;
        }

        ExpressionSyntax configExpression = node.ArgumentList.Arguments[0].Expression;
        if (!configExpression.TryExtractingConfig<CacheValueConfig>(context, "cache-value",
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
                "cache-value",
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

        XElement element = new("cache-value");
        if (!element.AddAttribute(config, nameof(CacheValueConfig.Key), "key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-value",
                nameof(CacheValueConfig.Key)
            ));
            return;
        }

        if (!element.AddAttribute(config, nameof(CacheValueConfig.VariableName), "variable-name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-value",
                nameof(CacheValueConfig.VariableName)
            ));
            return;
        }

        element.AddAttribute(config, nameof(CacheValueConfig.ExpiresAfter), "expires-after");
        element.AddAttribute(config, nameof(CacheValueConfig.RefreshAfter), "refresh-after");
        element.AddAttribute(config, nameof(CacheValueConfig.DefaultValue), "default-value");
        element.AddAttribute(config, nameof(CacheValueConfig.CachingType), "caching-type");

        XElement valueElement = new("value");
        var subContext = new DocumentCompilationContext(context, valueElement);
        _blockCompiler.Value.Compile(subContext, lambda.Block);
        element.Add(valueElement);

        context.AddPolicy(element);
    }
}
