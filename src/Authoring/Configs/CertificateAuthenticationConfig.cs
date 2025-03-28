// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the authentication-certificate policy that authenticates to a backend service using a client certificate.
/// This policy establishes a secure TLS channel with the backend and sends the client certificate as part of the TLS handshake.
/// </summary>
/// <remarks>
/// You must provide exactly one of the following: Thumbprint, CertificateId, or a combination of Body and Password.
/// </remarks>
public record CertificateAuthenticationConfig : IAuthenticationConfig
{
    /// <summary>
    /// Specifies the thumbprint of an SSL client certificate.
    /// The certificate must be added to the API Management service.
    /// </summary>
    /// <remarks>
    /// This property is mutually exclusive with CertificateId and Body+Password.
    /// </remarks>
    [ExpressionAllowed]
    public string? Thumbprint { get; init; }

    /// <summary>
    /// Specifies the resource identifier of an SSL client certificate.
    /// The certificate must be added to the API Management service.
    /// </summary>
    /// <remarks>
    /// This property is mutually exclusive with Thumbprint and Body+Password.
    /// </remarks>
    [ExpressionAllowed]
    public string? CertificateId { get; init; }

    /// <summary>
    /// Specifies the raw client certificate as a base64-encoded string.
    /// </summary>
    /// <remarks>
    /// This property must be used with Password and is mutually exclusive with Thumbprint and CertificateId.
    /// </remarks>
    [ExpressionAllowed]
    public byte[]? Body { get; init; }

    /// <summary>
    /// Specifies the password for the client certificate, if the certificate is password-protected.
    /// </summary>
    /// <remarks>
    /// This property must be used with Body and is mutually exclusive with Thumbprint and CertificateId.
    /// </remarks>
    [ExpressionAllowed]
    public string? Password { get; init; }
}