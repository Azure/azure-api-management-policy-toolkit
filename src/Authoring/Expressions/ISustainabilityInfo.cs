// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface ISustainabilityInfo
{
    CarbonIntensityCategory CurrentCarbonIntensity { get; }
}

/// <summary>
/// How carbon intensive the grid of the region is, as a category.
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
