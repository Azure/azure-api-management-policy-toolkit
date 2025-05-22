// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for Basic Authentication policy.<br />
/// Used with <a href="https://learn.microsoft.com/en-us/azure/api-management/authentication-basic-policy">authentication-basic</a> policy.
/// </summary>
public record BasicAuthenticationConfig : IAuthenticationConfig
{
    /// <summary>
    /// Username used for authentication.
    /// </summary>
    [ExpressionAllowed]
    public required string Username { get; init; }

    /// <summary>
    /// Password used for authentication.
    /// </summary>
    [ExpressionAllowed]
    public required string Password { get; init; }
}