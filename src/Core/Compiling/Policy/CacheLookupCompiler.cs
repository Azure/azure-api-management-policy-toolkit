// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class CacheLookupCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.CacheLookup);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<CacheLookupConfig>(context, "check-header", out var values))
        {
            return;
        }

        var element = new XElement("cache-lookup");

        if (!element.AddAttribute(values, nameof(CacheLookupConfig.VaryByDeveloper), "vary-by-developer"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-lookup",
                nameof(CacheLookupConfig.VaryByDeveloper)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(CacheLookupConfig.VaryByDeveloperGroups), "vary-by-developer-groups"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "cache-lookup",
                nameof(CacheLookupConfig.VaryByDeveloperGroups)
            ));
            return;
        }

        element.AddAttribute(values, nameof(CacheLookupConfig.CachingType), "caching-type");
        element.AddAttribute(values, nameof(CacheLookupConfig.DownstreamCachingType), "downstream-caching-type");
        element.AddAttribute(values, nameof(CacheLookupConfig.MustRevalidate), "must-revalidate");
        element.AddAttribute(values, nameof(CacheLookupConfig.AllowPrivateResponseCaching),
            "allow-private-response-caching");

        if (values.TryGetValue(nameof(CacheLookupConfig.VaryByHeaders), out var headers) &&
            headers.UnnamedValues is not null)
        {
            foreach (var value in headers.UnnamedValues)
            {
                element.Add(new XElement("vary-by-header", value.Value!));
            }
        }

        if (values.TryGetValue(nameof(CacheLookupConfig.VaryByQueryParameters), out var queryParams) &&
            queryParams.UnnamedValues is not null)
        {
            foreach (var value in queryParams.UnnamedValues)
            {
                element.Add(new XElement("vary-by-query-parameter", value.Value!));
            }
        }

        context.AddPolicy(element);
    }
}