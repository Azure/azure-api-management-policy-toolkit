// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-azure-ad-token policy.<br />
/// Specifies the parameters for validating an Azure Active Directory token.
/// </summary>
public record ValidateAzureAdTokenConfig
{
    /// <summary>
    /// The tenant ID of the Azure Active Directory.
    /// </summary>
    [ExpressionAllowed]
    public required string TenantId { get; init; }

    /// <summary>
    /// The name of the header containing the token.
    /// </summary>
    [ExpressionAllowed]
    public string? HeaderName { get; init; }

    /// <summary>
    /// The name of the query parameter containing the token.
    /// </summary>
    [ExpressionAllowed]
    public string? QueryParameterName { get; init; }

    /// <summary>
    /// The value of the token.
    /// </summary>
    [ExpressionAllowed]
    public string? TokenValue { get; init; }

    /// <summary>
    /// The HTTP status code to return if validation fails.
    /// </summary>
    [ExpressionAllowed]
    public int? FailedValidationHttpCode { get; init; }

    /// <summary>
    /// The error message to return if validation fails.
    /// </summary>
    [ExpressionAllowed]
    public string? FailedValidationErrorMessage { get; init; }

    /// <summary>
    /// The name of the variable to store the output token.
    /// </summary>
    public string? OutputTokenVariableName { get; init; }

    /// <summary>
    /// The application IDs of the backend services.
    /// </summary>
    public string[]? BackendApplicationIds { get; init; }

    /// <summary>
    /// The application IDs of the client applications.
    /// </summary>
    public string[]? ClientApplicationIds { get; init; }

    /// <summary>
    /// The expected audiences for the token.
    /// </summary>
    public string[]? Audiences { get; init; }

    /// <summary>
    /// The required claims for the token.
    /// </summary>
    public ClaimConfig[]? RequiredClaims { get; init; }

    /// <summary>
    /// The decryption keys for the token.
    /// </summary>
    public DecryptionKey[]? DecryptionKeys { get; init; }
}

/// <summary>
/// Configuration for a decryption key.
/// </summary>
public record DecryptionKey
{
    /// <summary>
    /// The ID of the certificate used for decryption.
    /// </summary>
    public required string CertificateId { get; init; }
}