// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the check-header policy, which checks for the existence and value of an HTTP header in the request.
/// </summary>
public record CheckHeaderConfig
{
    /// <summary>
    /// Specifies the name of the header to check.
    /// </summary>
    [ExpressionAllowed]
    public required string Name { get; init; }

    /// <summary>
    /// HTTP status code to return if the header check fails. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int FailCheckHttpCode { get; init; }

    /// <summary>
    /// Error message to return if the header check fails. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string FailCheckErrorMessage { get; init; }

    /// <summary>
    /// Indicates whether the header value comparison should ignore case. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required bool IgnoreCase { get; init; }

    /// <summary>
    /// Array of allowed header values. When omitted, only the presence of the header is checked.
    /// Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string[]? Values { get; init; }
}