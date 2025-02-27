// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// </summary>
public record RetryConfig
{
    /// <summary>
    ///     Specifies whether retries should be stopped (false) or continued (true). Policy expressions are allowed.
    /// </summary>
    public required bool Condition { get; init; }

    /// <summary>
    ///     A positive number between 1 and 50 specifying the number of retries to attempt. Policy expressions are allowed.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    ///     A positive number in seconds specifying the wait interval between the retry attempts. Policy expressions are
    ///     allowed.
    /// </summary>
    public int Interval { get; init; }

    /// <summary>
    ///     A positive number in seconds specifying the maximum wait interval between the retry attempts. It is used to
    ///     implement an exponential retry algorithm. Policy expressions are allowed.
    /// </summary>
    public int MaxInterval { get; init; }

    /// <summary>
    ///     A positive number in seconds specifying the wait interval increment. It is used to implement the linear and
    ///     exponential retry algorithms. Policy expressions are allowed.
    /// </summary>
    public int Delta { get; init; }

    /// <summary>
    ///     If set to true, the first retry attempt is performed immediately. Policy expressions are allowed.
    /// </summary>
    public bool FirstFastRetry { get; init; }
}