// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Supplies explicitly trusted OpenID or Microsoft Entra metadata and keys.
/// Register through <see cref="ServiceRegistry"/>; the emulator never fetches discovery documents or keys.
/// Implementations must scope the returned trust material to the requested policy and tenant.
/// </summary>
public interface IJwtKeyProvider
{
    /// <summary>Gets offline trust material, or throws <see cref="SecurityTokenException"/> on rejection.</summary>
    JwtTrustMaterial GetKeys(JwtKeyRequest request);
}

/// <summary>Identifies the discovery configuration whose trust material is being requested.</summary>
/// <param name="PolicyName">The authoring policy method name.</param>
/// <param name="TenantId">The authored Entra tenant ID or alias, or null for generic JWT validation.</param>
/// <param name="OpenIdConfigurationUrls">The authored discovery URLs; these are identifiers, not network requests.</param>
public sealed record JwtKeyRequest(
    string PolicyName, string? TenantId, IReadOnlyList<string> OpenIdConfigurationUrls);

/// <summary>Explicitly supplied offline keys and metadata. Empty keys do not establish trust.</summary>
public sealed record JwtTrustMaterial
{
    /// <summary>Trusted signature verification keys.</summary>
    public IReadOnlyList<SecurityKey> SigningKeys { get; init; } = [];

    /// <summary>Keys permitted to decrypt encrypted tokens.</summary>
    public IReadOnlyList<SecurityKey> DecryptionKeys { get; init; } = [];

    /// <summary>Trusted issuers advertised by the mocked discovery configuration.</summary>
    public IReadOnlyList<string> Issuers { get; init; } = [];

    /// <summary>
    /// Canonical Entra tenant GUID. Required to resolve a domain or URL alias to a token's tenant.
    /// For common or organizations, omit this value unless intentionally restricting to one tenant.
    /// </summary>
    public string? TenantId { get; init; }
}
