// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Acquires authorization contexts for the get-authorization-context policy.
/// Register an implementation through <see cref="ServiceRegistry"/>; the emulator never contacts an identity service.
/// </summary>
public interface IAuthorizationProvider
{
    /// <summary>
    /// Gets the authorization for a connection after checking its access policy.
    /// Implementations control token acquisition and identity validation, including JWT validation.
    /// </summary>
    /// <param name="request">The resolved connection and identity information.</param>
    /// <param name="cancellationToken">A token for cancelling acquisition.</param>
    /// <returns>An authorization containing a nonempty access token and a nonnull claims dictionary.</returns>
    /// <exception cref="HttpRequestException">The credential provider could not acquire authorization.</exception>
    /// <exception cref="UnauthorizedAccessException">The identity is not allowed to access the connection.</exception>
    /// <exception cref="ArgumentException">The identity token or connection information is invalid.</exception>
    /// <exception cref="SecurityTokenException">The identity token failed validation.</exception>
    /// <exception cref="InvalidOperationException">The provider cannot return a valid authorization.</exception>
    /// <exception cref="TimeoutException">Authorization acquisition timed out.</exception>
    /// <exception cref="OperationCanceledException">Authorization acquisition was cancelled.</exception>
    Task<Authorization> GetAuthorizationAsync(
        AuthorizationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolved inputs for acquiring a connection's authorization context.
/// </summary>
/// <param name="ProviderId">The credential provider resource identifier.</param>
/// <param name="AuthorizationId">The connection resource identifier.</param>
/// <param name="IdentityType">The identity type: managed or jwt.</param>
/// <param name="Identity">The JWT identity token, or null for managed identity.</param>
public sealed record AuthorizationRequest(
    string ProviderId, string AuthorizationId, string IdentityType, string? Identity);
