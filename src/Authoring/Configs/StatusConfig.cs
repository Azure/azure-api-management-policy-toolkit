// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration to set the HTTP status code and reason to the specified value.<br />
/// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-status-policy">set-status</a> policy.
/// </summary>
public record StatusConfig
{
    /// <summary>
    /// The HTTP status code to return. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int Code { get; init; }

    /// <summary>
    /// A description of the reason for returning the status code. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Reason { get; init; }
};