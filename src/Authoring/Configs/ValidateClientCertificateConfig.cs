// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-client-certificate policy.
/// </summary>
public record ValidateClientCertificateConfig
{
    /// <summary>
    /// Specifies whether to validate the certificate revocation status.
    /// </summary>
    public bool? ValidateRevocation { get; init; }

    /// <summary>
    /// Specifies whether to validate the certificate trust chain.
    /// </summary>
    public bool? ValidateTrust { get; init; }

    /// <summary>
    /// Specifies whether to validate the certificate's not-before date.
    /// </summary>
    public bool? ValidateNotBefore { get; init; }

    /// <summary>
    /// Specifies whether to validate the certificate's not-after date.
    /// </summary>
    public bool? ValidateNotAfter { get; init; }

    /// <summary>
    /// Specifies whether to ignore validation errors.
    /// </summary>
    public bool? IgnoreError { get; init; }

    /// <summary>
    /// Specifies the identities to validate against.
    /// </summary>
    public CertificateIdentity[]? Identities { get; init; }
}

/// <summary>
/// Represents a certificate identity for validation.
/// </summary>
public record CertificateIdentity
{
    /// <summary>
    /// The thumbprint of the certificate.
    /// </summary>
    public string? Thumbprint { get; init; }

    /// <summary>
    /// The serial number of the certificate.
    /// </summary>
    public string? SerialNumber { get; init; }

    /// <summary>
    /// The common name of the certificate.
    /// </summary>
    public string? CommonName { get; init; }

    /// <summary>
    /// The subject of the certificate.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// The DNS name of the certificate.
    /// </summary>
    public string? DnsName { get; init; }

    /// <summary>
    /// The issuer subject of the certificate.
    /// </summary>
    public string? IssuerSubject { get; init; }

    /// <summary>
    /// The issuer thumbprint of the certificate.
    /// </summary>
    public string? IssuerThumbprint { get; init; }

    /// <summary>
    /// The issuer certificate ID.
    /// </summary>
    public string? IssuerCertificateId { get; init; }
}