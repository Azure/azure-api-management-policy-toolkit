// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class EmitMetricCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.EmitMetric);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<EmitMetricConfig>(context, "emit-metric", out var values))
        {
            return;
        }

        var element = new XElement("emit-metric");
        if (!element.AddAttribute(values, nameof(EmitMetricConfig.Name), "name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "emit-metric",
                nameof(EmitMetricConfig.Name)
            ));
            return;
        }

        element.AddAttribute(values, nameof(EmitMetricConfig.Value), "value");
        element.AddAttribute(values, nameof(EmitMetricConfig.Namespace), "namespace");

        if (!values.TryGetValue(nameof(EmitMetricConfig.Dimensions), out var dimensionsInitializer))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "emit-metric",
                nameof(EmitMetricConfig.Dimensions)
            ));
            return;
        }

        var dimensions = dimensionsInitializer.UnnamedValues ?? Array.Empty<InitializerValue>();
        if (dimensions.Count == 0)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterIsEmpty,
                dimensionsInitializer.Node.GetLocation(),
                "emit-metric",
                nameof(EmitMetricConfig.Dimensions)
            ));
            return;
        }

        foreach (var dimension in dimensions)
        {
            if (!dimension.TryGetValues<MetricDimensionConfig>(out var result))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    dimension.Node.GetLocation(),
                    "emit-metric.dimension",
                    nameof(MetricDimensionConfig)
                ));
                continue;
            }

            var dimensionElement = new XElement("dimension");
            if (!dimensionElement.AddAttribute(result, nameof(MetricDimensionConfig.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    node.GetLocation(),
                    "emit-metric.dimension",
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