// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class IpFilterCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.IpFilter);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<IpFilterConfig>(context, "ip-filter", out var values))
        {
            return;
        }

        var element = new XElement("ip-filter");

        if (!element.AddAttribute(values, nameof(IpFilterConfig.Action), "action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "ip-filter",
                nameof(IpFilterConfig.Action)
            ));
            return;
        }

        bool atLeastOneAddress = false;
        if (values.TryGetValue(nameof(IpFilterConfig.Addresses), out var addresses))
        {
            foreach (var address in addresses.UnnamedValues!)
            {
                element.Add(new XElement("address", address.Value!));
                atLeastOneAddress = true;
            }
        }

        bool atLeastOneRange = false;
        if (values.TryGetValue(nameof(IpFilterConfig.AddressRanges), out var ranges))
        {
            foreach (var range in ranges.UnnamedValues!)
            {
                if (range.Type != nameof(AddressRange))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                        range.Node.GetLocation(),
                        "ip-filter.address-range",
                        nameof(AddressRange)
                    ));
                    continue;
                }

                var rangeValues = range.NamedValues;
                if (rangeValues is null)
                {
                    return;
                }

                var rangeElement = new XElement("address-range");
                if (!rangeElement.AddAttribute(rangeValues, nameof(AddressRange.From), "from"))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.RequiredParameterNotDefined,
                        range.Node.GetLocation(),
                        "ip-filter.address-range",
                        nameof(AddressRange.From)
                    ));
                    continue;
                }

                if (!rangeElement.AddAttribute(rangeValues, nameof(AddressRange.To), "to"))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.RequiredParameterNotDefined,
                        range.Node.GetLocation(),
                        "ip-filter.address-range",
                        nameof(AddressRange.To)
                    ));
                    continue;
                }

                element.Add(rangeElement);

                atLeastOneRange = true;
            }
        }

        if (!atLeastOneAddress && !atLeastOneRange)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.AtLeastOneOfTwoShouldBeDefined,
                node.GetLocation(),
                "ip-filter",
                nameof(IpFilterConfig.Addresses),
                nameof(IpFilterConfig.AddressRanges)
            ));
            return;
        }

        context.AddPolicy(element);
    }
}