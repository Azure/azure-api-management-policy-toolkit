// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface ISustainabilityInfo
{
    CarbonIntensityCategory CurrentCarbonIntensity { get; }
}

/// <summary>
/// Carbon intensity of the region's grid, in grams of CO2 equivalent per kWh.
/// </summary>
public enum CarbonIntensityCategory
{
    NotAvailable,
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}
