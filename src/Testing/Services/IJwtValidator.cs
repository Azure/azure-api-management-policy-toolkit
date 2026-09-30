// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Authenticates and decrypts tokens for the identity validation policies.
/// Register through <see cref="ServiceRegistry"/>. Implementations are an explicit trust boundary:
/// they must verify every signed token, even when unsigned tokens are allowed, and must reject
/// unsigned tokens when required. The handlers independently enforce lifetime and policy claims.
/// </summary>
public interface IJwtValidator
{
    /// <summary>
    /// Returns the authenticated token, or throws <see cref="SecurityTokenException"/> on rejection.
    /// Unexpected provider failures must be thrown rather than converted to authenticated tokens.
    /// </summary>
    Jwt Validate(JwtValidationRequest request);
}

/// <summary>
/// Resolved, offline inputs for authenticating a token. Keys contain only explicitly supplied trust material.
/// </summary>
/// <param name="PolicyName">The authoring policy method name.</param>
/// <param name="Token">The compact token without an HTTP authentication scheme.</param>
/// <param name="RequireSignedTokens">Whether unsigned tokens must be rejected.</param>
/// <param name="SigningKeys">Trusted signature verification keys.</param>
/// <param name="DecryptionKeys">Keys permitted to decrypt the token.</param>
/// <param name="ValidationTime">The gateway request timestamp plus elapsed time.</param>
public sealed record JwtValidationRequest(
    string PolicyName,
    string Token,
    bool RequireSignedTokens,
    IReadOnlyList<SecurityKey> SigningKeys,
    IReadOnlyList<SecurityKey> DecryptionKeys,
    DateTimeOffset ValidationTime);
