// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the retry policy, specifying retry conditions, retry count, intervals, and other retry behavior settings.
/// </summary>
public record RetryConfig
{
    /// <summary>
    /// Specifies whether retries should be stopped (false) or continued (true). Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required bool Condition { get; init; }

    /// <summary>
    /// A positive number between 1 and 50 specifying the number of retries to attempt. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int Count { get; init; }

    /// <summary>
    /// A positive number in seconds specifying the wait interval between retry attempts. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? Interval { get; init; }

    /// <summary>
    /// A positive number in seconds specifying the maximum wait interval between retry attempts. Used to implement an exponential retry algorithm. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? MaxInterval { get; init; }

    /// <summary>
    /// A positive number in seconds specifying the wait interval increment. Used to implement linear and exponential retry algorithms. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public int? Delta { get; init; }

    /// <summary>
    /// If set to true, the first retry attempt is performed immediately. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? FirstFastRetry { get; init; }
}