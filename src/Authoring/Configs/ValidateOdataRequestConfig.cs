// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-odata-request policy.
/// </summary>
public record ValidateOdataRequestConfig
{
    /// <summary>
    /// Specifies the name of the variable to store error details if validation fails.
    /// </summary>
    public string? ErrorVariableName { get; init; }

    /// <summary>
    /// Specifies the default OData version to use if not specified in the request.
    /// </summary>
    public string? DefaultOdataVersion { get; init; }

    /// <summary>
    /// Specifies the minimum OData version that the request must comply with.
    /// </summary>
    public string? MinOdataVersion { get; init; }

    /// <summary>
    /// Specifies the maximum OData version that the request can comply with.
    /// </summary>
    public string? MaxOdataVersion { get; init; }

    /// <summary>
    /// Specifies the maximum size of the request payload.
    /// </summary>
    public int? MaxSize { get; init; }
}