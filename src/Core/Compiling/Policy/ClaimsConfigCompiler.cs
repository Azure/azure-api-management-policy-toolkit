// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public static class ClaimsConfigCompiler
{
    public static XElement HandleRequiredClaims(ICompilationContext context, InitializerValue requiredClaims)
    {
        XElement claimsElement = new("required-claims");
        foreach (InitializerValue claim in requiredClaims.UnnamedValues ?? [])
        {
            if (!claim.TryGetValues<ClaimConfig>(out IReadOnlyDictionary<string, InitializerValue>? claimValue))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    claim.Node.GetLocation(),
                    "required-claims",
                    nameof(ClaimConfig)
                ));
                continue;
            }

            XElement claimElement = new("claim");
            if (!claimElement.AddAttribute(claimValue, nameof(ClaimConfig.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    claim.Node.GetLocation(),
                    "claim",
                    nameof(ClaimConfig.Name)
                ));
                continue;
            }

            claimElement.AddAttribute(claimValue, nameof(ClaimConfig.Match), "match");
            claimElement.AddAttribute(claimValue, nameof(ClaimConfig.Separator), "separator");

            if (claimValue.TryGetValue(nameof(ClaimConfig.Values), out InitializerValue? valuesInitializer))
            {
                foreach (InitializerValue value in valuesInitializer.UnnamedValues ?? [])
                {
                    claimElement.Add(new XElement("value", value.Value!));
                }
            }

            claimsElement.Add(claimElement);
        }

        return claimsElement;
    }
}