// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CacheStoreValueCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.CacheStoreValue);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<CacheStoreValueConfig>(context, "cache-store-value", out var values))
        {
            return;
        }

        var element = new XElement("cache-store-value");

        if (!element.AddAttribute(values, nameof(CacheStoreValueConfig.Key), "key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-store-value",
                nameof(CacheStoreValueConfig.Key)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CacheStoreValueConfig.Value), "value"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-store-value",
                nameof(CacheStoreValueConfig.Value)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CacheStoreValueConfig.Duration), "duration"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-store-value",
                nameof(CacheStoreValueConfig.Duration)
            ));
            return;
        }

        element.AddAttribute(values, nameof(CacheStoreValueConfig.CachingType), "caching-type");

        context.AddPolicy(element);
    }
}