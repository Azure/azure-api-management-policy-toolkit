// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the get-authorization-context policy.
/// </summary>
public record GetAuthorizationContextConfig
{
    /// <summary>
    /// The credential provider resource identifier. Policy expressions are allowed.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// The connection resource identifier. Policy expressions are allowed.
    /// </summary>
    public required string AuthorizationId { get; init; }

    /// <summary>
    /// The name of the context variable to receive the Authorization object. Policy expressions are allowed.
    /// </summary>
    public required string ContextVariableName { get; init; }

    /// <summary>
    /// Type of identity to check against the connection's access policy. Default is "managed". Policy expressions are allowed.
    /// </summary>
    public string? IdentityType { get; init; } = "managed";

    /// <summary>
    /// A Microsoft Entra JWT bearer token to check against the connection permissions. Ignored for identity-type other than jwt. Policy expressions are allowed.
    /// </summary>
    public string? Identity { get; init; }

    /// <summary>
    /// Boolean. If acquiring the authorization context results in an error, the context variable is assigned a value of null if true, otherwise return 500. Default is false. Policy expressions are allowed.
    /// </summary>
    public bool? IgnoreError { get; init; } = false;
}