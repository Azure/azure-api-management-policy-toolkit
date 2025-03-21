// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record ValidateClientCertificateConfig
{
    public bool? ValidateRevocation { get; init; }
    public bool? ValidateTrust { get; init; }
    public bool? ValidateNotBefore { get; init; }
    public bool? ValidateNotAfter { get; init; }
    public bool? IgnoreError { get; init; }

    public CertificateIdentity[]? Identities { get; init; }
}

public record CertificateIdentity
{
    public string? Thumbprint { get; init; }
    public string? SerialNumber { get; init; }
    public string? CommonName { get; init; }
    public string? Subject { get; init; }
    public string? DnsName { get; init; }
    public string? IssuerSubject { get; init; }
    public string? IssuerThumbprint { get; init; }
    public string? IssuerCertificateId { get; init; }
}