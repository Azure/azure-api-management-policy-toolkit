// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class QuotaByKeyCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.QuotaByKey);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<QuotaByKeyConfig>(context, "quota-by-key",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("quota-by-key");

        if (!element.AddAttribute(values, nameof(QuotaByKeyConfig.CounterKey), "counter-key"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "quota-by-key",
                nameof(QuotaByKeyConfig.CounterKey)
            ));
            return;
        }

        bool isCallsAdded = element.AddAttribute(values, nameof(QuotaByKeyConfig.Calls), "calls");
        bool isBandwidthAdded = element.AddAttribute(values, nameof(QuotaByKeyConfig.Bandwidth), "bandwidth");

        if (!isCallsAdded && !isBandwidthAdded)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.AtLeastOneOfTwoShouldBeDefined,
                node.GetLocation(),
                "quota-by-key",
                nameof(QuotaByKeyConfig.Calls),
                nameof(QuotaByKeyConfig.Bandwidth)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(QuotaByKeyConfig.RenewalPeriod), "renewal-period"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "quota-by-key",
                nameof(QuotaByKeyConfig.RenewalPeriod)
            ));
            return;
        }

        element.AddAttribute(values, nameof(QuotaByKeyConfig.IncrementCondition), "increment-condition");
        element.AddAttribute(values, nameof(QuotaByKeyConfig.IncrementCount), "increment-count");
        element.AddAttribute(values, nameof(QuotaByKeyConfig.FirstPeriodStart), "first-period-start");

        context.AddPolicy(element);
    }
}