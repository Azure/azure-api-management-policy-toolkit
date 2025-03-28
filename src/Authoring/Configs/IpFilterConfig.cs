// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the IpFilter policy.<br/>
/// Specifies the action to take (allow or deny), IP addresses, and/or IP address ranges.
/// </summary>
public record IpFilterConfig
{
    /// <summary>
    /// Specifies the action to take (allow or deny). Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Action { get; init; }

    /// <summary>
    /// Specifies the IP addresses to filter. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string[]? Addresses { get; init; }

    /// <summary>
    /// Specifies the IP address ranges to filter.
    /// </summary>
    public AddressRange[]? AddressRanges { get; init; }
}

/// <summary>
/// Represents an IP address range.
/// </summary>
public record AddressRange
{
    /// <summary>
    /// Specifies the starting IP address of the range. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string From { get; init; }

    /// <summary>
    /// Specifies the ending IP address of the range. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string To { get; init; }
}