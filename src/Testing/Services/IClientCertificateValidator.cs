// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Validates client certificate trust and revocation using explicit mocked trust material.
/// Register through <see cref="ServiceRegistry"/>. The emulator does not contact certificate authorities
/// or infer trust from a certificate's presence in the general certificate store.
/// </summary>
public interface IClientCertificateValidator
{
    /// <summary>
    /// Returns trust/revocation evidence, or throws <see cref="SecurityException"/> on rejection.
    /// An issuer certificate, when returned, must be the cryptographically verified immediate issuer
    /// of the requested certificate, not merely a certificate with a matching subject name.
    /// Handlers independently check the requested validity flags and certificate identities.
    /// </summary>
    ClientCertificateValidationResult Validate(ClientCertificateValidationRequest request);
}

/// <summary>Resolved certificate validation requirements and the deterministic gateway clock.</summary>
/// <param name="Certificate">The certificate presented by the client.</param>
/// <param name="ValidationTime">The gateway request timestamp plus elapsed time.</param>
/// <param name="ValidateTrust">Whether trusted-chain evidence is required.</param>
/// <param name="ValidateRevocation">Whether affirmative revocation evidence is required.</param>
/// <param name="ValidateNotBefore">Whether the not-before date is enforced.</param>
/// <param name="ValidateNotAfter">Whether the not-after date is enforced.</param>
public sealed record ClientCertificateValidationRequest(
    X509Certificate2 Certificate,
    DateTimeOffset ValidationTime,
    bool ValidateTrust,
    bool ValidateRevocation,
    bool ValidateNotBefore,
    bool ValidateNotAfter);

/// <summary>Revocation evidence. Unknown is not equivalent to a successful check.</summary>
public enum ClientCertificateRevocationStatus
{
    Unknown,
    Good,
    Revoked
}

/// <summary>Explicit validator evidence; the default result establishes neither trust nor revocation status.</summary>
public sealed record ClientCertificateValidationResult
{
    /// <summary>Whether the certificate chain reaches an explicitly trusted authority.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>The mocked revocation status.</summary>
    public ClientCertificateRevocationStatus RevocationStatus { get; init; }

    /// <summary>The cryptographically verified immediate issuer, if available.</summary>
    public X509Certificate2? IssuerCertificate { get; init; }
}
