// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateJwtConfig
{
    public string? HeaderName { get; init; }
    public string? QueryParameterName { get; init; }
    public string? TokenValue { get; init; }

    public int? FailedValidationHttpCode { get; init; }
    public string? FailedValidationErrorMessage { get; init; }
    public bool? RequireExpirationTime { get; init; }
    public string? RequireScheme { get; init; }
    public bool? RequireSignedTokens { get; init; }
    public int? ClockSkew { get; init; }
    public string? OutputTokenVariableName { get; init; }

    public OpenIdConfig[]? OpenIdConfigs { get; init; }
    public KeyConfig[]? IssuerSigningKeys { get; init; }
    public KeyConfig[]? DescriptionKeys { get; init; }
    public string[]? Audiences { get; init; }
    public string[]? Issuers { get; init; }
    public ClaimConfig[]? RequiredClaims { get; init; }
}

public record OpenIdConfig
{
    public required string Url { get; init; }
}

public abstract record KeyConfig
{
    public string? Id { get; init; }
}

public sealed record Base64KeyConfig : KeyConfig
{
    public required string Value { get; init; }
}

public sealed record CertificateKeyConfig : KeyConfig
{
    public required string CertificateId { get; init; }
}

public sealed record AsymmetricKeyConfig : KeyConfig
{
    public required string Modulus { get; init; }
    public required string Exponent { get; init; }
}