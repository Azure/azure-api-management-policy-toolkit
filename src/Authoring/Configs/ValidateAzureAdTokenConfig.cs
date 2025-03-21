// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateAzureAdTokenConfig
{
    public required string TenantId { get; init; }

    public string? HeaderName { get; init; }
    public string? QueryParameterName { get; init; }
    public string? TokenValue { get; init; }
    public int? FailedValidationHttpCode { get; init; }
    public string? FailedValidationErrorMessage { get; init; }
    public string? OutputTokenVariableName { get; init; }
    public bool? AllowProtectedForwardedTokens { get; init; }
    public bool? AllowProofOfPossessionTokes { get; init; }
    public string? OutputPftTokenVariableName { get; init; }

    public string[]? BackendApplicationIds { get; init; }
    public string[]? ClientApplicationIds { get; init; }
    public string[]? Audiences { get; init; }
    public ClaimConfig[]? RequiredClaims { get; init; }
    public DecryptionKey[]? DecryptionKeys { get; init; }
}

public record DecryptionKey
{
    public required string CertificateId { get; init; }
}