// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the ValidateJwt policy.
/// </summary>
public record ValidateJwtConfig
{
    /// <summary>
    /// Specifies the name of the HTTP header that contains the JWT.
    /// </summary>
    [ExpressionAllowed]
    public string? HeaderName { get; init; }

    /// <summary>
    /// Specifies the name of the query parameter that contains the JWT.
    /// </summary>
    [ExpressionAllowed]
    public string? QueryParameterName { get; init; }

    /// <summary>
    /// Specifies the JWT token value directly.
    /// </summary>
    [ExpressionAllowed]
    public string? TokenValue { get; init; }

    /// <summary>
    /// Specifies the HTTP status code to return if validation fails.
    /// </summary>
    [ExpressionAllowed]
    public int? FailedValidationHttpCode { get; init; }

    /// <summary>
    /// Specifies the error message to return if validation fails.
    /// </summary>
    [ExpressionAllowed]
    public string? FailedValidationErrorMessage { get; init; }

    /// <summary>
    /// Specifies whether the token must have an expiration time.
    /// </summary>
    [ExpressionAllowed]
    public bool? RequireExpirationTime { get; init; }

    /// <summary>
    /// Specifies the required scheme for the token.
    /// </summary>
    [ExpressionAllowed]
    public string? RequireScheme { get; init; }

    /// <summary>
    /// Specifies whether the token must be signed.
    /// </summary>
    [ExpressionAllowed]
    public bool? RequireSignedTokens { get; init; }

    /// <summary>
    /// Specifies the allowed clock skew in seconds.
    /// </summary>
    [ExpressionAllowed]
    public int? ClockSkew { get; init; }

    /// <summary>
    /// Specifies the name of the variable to store the validated token.
    /// </summary>
    public string? OutputTokenVariableName { get; init; }

    /// <summary>
    /// Specifies the OpenID Connect configurations.
    /// </summary>
    public OpenIdConfig[]? OpenIdConfigs { get; init; }

    /// <summary>
    /// Specifies the issuer signing keys.
    /// </summary>
    public KeyConfig[]? IssuerSigningKeys { get; init; }

    /// <summary>
    /// Specifies the description keys.
    /// </summary>
    public KeyConfig[]? DescriptionKeys { get; init; }

    /// <summary>
    /// Specifies the allowed audiences.
    /// </summary>
    [ExpressionAllowed]
    public string[]? Audiences { get; init; }

    /// <summary>
    /// Specifies the allowed issuers.
    /// </summary>
    [ExpressionAllowed]
    public string[]? Issuers { get; init; }

    /// <summary>
    /// Specifies the required claims.
    /// </summary>
    public ClaimConfig[]? RequiredClaims { get; init; }
}

/// <summary>
/// Configuration for OpenID Connect.
/// </summary>
public record OpenIdConfig
{
    /// <summary>
    /// Specifies the URL of the OpenID Connect configuration.
    /// </summary>
    public required string Url { get; init; }
}

/// <summary>
/// Abstract base class for key configurations.
/// </summary>
public abstract record KeyConfig
{
    /// <summary>
    /// Specifies the identifier of the key.
    /// </summary>
    public string? Id { get; init; }
}

/// <summary>
/// Configuration for a base64-encoded key.
/// </summary>
public sealed record Base64KeyConfig : KeyConfig
{
    /// <summary>
    /// Specifies the base64-encoded value of the key.
    /// </summary>
    public required string Value { get; init; }
}

/// <summary>
/// Configuration for a certificate key.
/// </summary>
public sealed record CertificateKeyConfig : KeyConfig
{
    /// <summary>
    /// Specifies the identifier of the certificate.
    /// </summary>
    public required string CertificateId { get; init; }
}

/// <summary>
/// Configuration for an asymmetric key.
/// </summary>
public sealed record AsymmetricKeyConfig : KeyConfig
{
    /// <summary>
    /// Specifies the modulus of the asymmetric key.
    /// </summary>
    public required string Modulus { get; init; }

    /// <summary>
    /// Specifies the exponent of the asymmetric key.
    /// </summary>
    public required string Exponent { get; init; }
}