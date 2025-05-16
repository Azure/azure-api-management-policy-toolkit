// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CacheLookupValueCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.CacheLookupValue);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<CacheLookupValueConfig>(context, "cache-lookup-value", out var values))
        {
            return;
        }

        var element = new XElement("cache-lookup-value");

        if (!element.AddAttribute(values, nameof(CacheLookupValueConfig.Key), "key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-lookup-value",
                nameof(CacheLookupValueConfig.Key)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CacheLookupValueConfig.VariableName), "variable-name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-lookup-value",
                nameof(CacheLookupValueConfig.VariableName)
            ));
            return;
        }

        element.AddAttribute(values, nameof(CacheLookupValueConfig.CachingType), "caching-type");
        element.AddAttribute(values, nameof(CacheLookupValueConfig.DefaultValue), "default-value");

        context.AddPolicy(element);
    }
}