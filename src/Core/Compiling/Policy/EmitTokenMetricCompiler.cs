// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class LlmEmitTokenMetricCompiler()
    : BaseEmitTokenMetricCompiler("llm-emit-token-metric", nameof(IInboundContext.LlmEmitTokenMetric));

public class AzureOpenAiEmitTokenMetricCompiler()
    : BaseEmitTokenMetricCompiler("azure-openai-emit-token-metric", nameof(IInboundContext.AzureOpenAiEmitTokenMetric));

public abstract class BaseEmitTokenMetricCompiler : IMethodPolicyHandler
{
    private readonly string _policyName;
    public string MethodName { get; }

    protected BaseEmitTokenMetricCompiler(string policyName, string methodName)
    {
        this._policyName = policyName;
        MethodName = methodName;
    }

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<EmitTokenMetricConfig>(context, _policyName, out var values))
        {
            return;
        }

        var element = new XElement(_policyName);

        element.AddAttribute(values, nameof(EmitTokenMetricConfig.Namespace), "namespace");

        if (!values.TryGetValue(nameof(EmitTokenMetricConfig.Dimensions), out var dimensionsInitializer))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                _policyName,
                nameof(EmitTokenMetricConfig.Dimensions)
            ));
            return;
        }

        var dimensions = dimensionsInitializer.UnnamedValues ?? Array.Empty<InitializerValue>();
        if (dimensions.Count == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                dimensionsInitializer.Node.GetLocation(),
                _policyName,
                nameof(EmitTokenMetricConfig.Dimensions)
            ));
            return;
        }

        foreach (var dimension in dimensions)
        {
            if (!dimension.TryGetValues<MetricDimensionConfig>(out var result))
            {
                continue;
            }

            var dimensionElement = new XElement("dimension");
            if (!dimensionElement.AddAttribute(result, nameof(MetricDimensionConfig.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    dimension.Node.GetLocation(),
                    $"{_policyName}.dimension",
                    nameof(MetricDimensionConfig.Name)
                ));
                continue;
            }

            dimensionElement.AddAttribute(result, nameof(MetricDimensionConfig.Value), "value");
            element.Add(dimensionElement);
        }

        context.AddPolicy(element);
    }
}