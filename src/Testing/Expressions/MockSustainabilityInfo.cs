// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockSustainabilityInfo : ISustainabilityInfo
{
    public CarbonIntensityCategory CurrentCarbonIntensity { get; set; } = CarbonIntensityCategory.NotAvailable;
}
