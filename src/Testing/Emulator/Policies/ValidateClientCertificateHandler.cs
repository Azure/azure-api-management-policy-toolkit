// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Formats.Asn1;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateClientCertificateHandler : PolicyHandler<ValidateClientCertificateConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateClientCertificate);

    protected override void Handle(GatewayContext context, ValidateClientCertificateConfig config)
    {
        ValidateConfig(config);
        try
        {
            Validate(context, config);
        }
        catch (SecurityException error)
        {
            Fail(context, config, error is CertificateValidationFailure failure
                ? failure.Reason : "ClientCertificateValidationFailed", error.Message);
        }
        catch (CryptographicException)
        {
            Fail(context, config, "ClientCertificateValidationFailed", "Client certificate cryptographic validation failed.");
        }
        catch (AsnContentException)
        {
            Fail(context, config, "ClientCertificateValidationFailed", "Client certificate identity data is malformed.");
        }
    }

    private static void Validate(GatewayContext context, ValidateClientCertificateConfig config)
    {
        var certificate = context.Request.Certificate
            ?? throw new CertificateValidationFailure("ClientCertificateNotPresent", "Client certificate is not present.");
        var time = IdentityTokenValidator.ValidationTime(context);
        if (config.ValidateNotBefore != false && time.UtcDateTime < certificate.NotBefore.ToUniversalTime())
        {
            throw new CertificateValidationFailure("ClientCertificateNotYetValid", "Client certificate is not yet valid.");
        }

        if (config.ValidateNotAfter != false && time.UtcDateTime > certificate.NotAfter.ToUniversalTime())
        {
            throw new CertificateValidationFailure("ClientCertificateExpired", "Client certificate has expired.");
        }

        var validator = context.Services.Resolve<IClientCertificateValidator>(nameof(IInboundContext.ValidateClientCertificate))
            ?? context.Services.Resolve<IClientCertificateValidator>();
        ClientCertificateValidationResult evidence;
        if (validator is null)
        {
            if (config.ValidateTrust != false || config.ValidateRevocation != false)
            {
                throw new CertificateValidationFailure("ClientCertificateTrustUnavailable",
                    "Client certificate trust and revocation require an offline IClientCertificateValidator.");
            }

            evidence = new ClientCertificateValidationResult();
        }
        else
        {
            evidence = validator.Validate(new ClientCertificateValidationRequest(certificate, time,
                config.ValidateTrust ?? true, config.ValidateRevocation ?? true,
                config.ValidateNotBefore ?? true, config.ValidateNotAfter ?? true))
                ?? throw new InvalidOperationException("The client certificate validator returned no validation evidence.");
        }

        if (config.ValidateTrust != false && !evidence.IsTrusted)
        {
            throw new CertificateValidationFailure("ClientCertificateUntrusted", "Client certificate is not trusted.");
        }

        if (config.ValidateRevocation != false && evidence.RevocationStatus != ClientCertificateRevocationStatus.Good)
        {
            throw evidence.RevocationStatus == ClientCertificateRevocationStatus.Revoked
                ? new CertificateValidationFailure("ClientCertificateRevoked", "Client certificate is revoked.")
                : new CertificateValidationFailure("ClientCertificateRevocationUnknown", "Client certificate revocation status is unknown.");
        }

        if (config.Identities is null || config.Identities.Length == 0) { return; }
        string? missingIssuer = null;
        foreach (var identity in config.Identities)
        {
            if (!MatchesLocalIdentity(certificate, identity)) { continue; }
            var issuer = evidence.IssuerCertificate;
            if (identity.IssuerThumbprint is not null
                && (issuer is null || !MatchesHex(issuer.Thumbprint, identity.IssuerThumbprint)
                    || !MatchesName(certificate.Issuer, issuer.Subject)))
            {
                continue;
            }

            if (identity.IssuerCertificateId is not null)
            {
                var expected = IdentityTokenValidator.FindCertificate(context, identity.IssuerCertificateId);
                if (expected is null)
                {
                    missingIssuer = identity.IssuerCertificateId;
                    continue;
                }

                if (issuer is null || !issuer.PublicKey.ExportSubjectPublicKeyInfo().AsSpan()
                        .SequenceEqual(expected.PublicKey.ExportSubjectPublicKeyInfo())
                    || !MatchesName(certificate.Issuer, issuer.Subject))
                {
                    continue;
                }
            }

            return;
        }

        throw new CertificateValidationFailure("ClientCertificateIdentityNotAllowed", missingIssuer is null
            ? "Client certificate does not match any configured identity."
            : $"The configured issuer certificate '{missingIssuer}' is missing.");
    }

    private static bool MatchesLocalIdentity(X509Certificate2 certificate, CertificateIdentity identity) =>
        (identity.Thumbprint is null || MatchesHex(certificate.Thumbprint, identity.Thumbprint))
        && (identity.SerialNumber is null || MatchesHex(certificate.SerialNumber, identity.SerialNumber))
        && (identity.Subject is null || MatchesName(certificate.Subject, identity.Subject))
        && (identity.IssuerSubject is null || MatchesName(certificate.Issuer, identity.IssuerSubject))
        && (identity.CommonName is null || MatchesCommonName(certificate, identity.CommonName))
        && (identity.DnsName is null || MatchesDnsName(certificate, identity.DnsName));

    private static bool MatchesCommonName(X509Certificate2 certificate, string expected)
    {
        var names = new List<string>();
        var reader = new AsnReader(certificate.SubjectName.RawData, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        while (sequence.HasData)
        {
            var attributes = sequence.ReadSetOf();
            while (attributes.HasData)
            {
                var attribute = attributes.ReadSequence();
                var oid = attribute.ReadObjectIdentifier();
                if (oid == "2.5.4.3")
                {
                    var tag = attribute.PeekTag();
                    names.Add(attribute.ReadCharacterString((UniversalTagNumber)tag.TagValue));
                }
                else
                {
                    attribute.ReadEncodedValue();
                }

                attribute.ThrowIfNotEmpty();
            }
        }

        reader.ThrowIfNotEmpty();
        return names.Count == 1 && MatchesName(names[0], expected);
    }

    private static bool MatchesDnsName(X509Certificate2 certificate, string expected)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value != "2.5.29.17") { continue; }
            var names = new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);
            if (names.EnumerateDnsNames().Any(name => MatchesName(name, expected))) { return true; }
        }

        return false;
    }

    private static bool MatchesName(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesHex(string actual, string expected) => string.Equals(
        actual.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal),
        expected.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal),
        StringComparison.OrdinalIgnoreCase);

    private static void ValidateConfig(ValidateClientCertificateConfig config)
    {
        if (config.Identities is null) { return; }
        if (config.Identities.Length > 10)
        {
            throw new ArgumentException("Client certificate validation supports at most ten identities.", nameof(config.Identities));
        }

        foreach (var identity in config.Identities)
        {
            if (identity.IssuerCertificateId is not null
                && (identity.IssuerSubject is not null || identity.IssuerThumbprint is not null))
            {
                throw new ArgumentException("IssuerCertificateId is mutually exclusive with other issuer attributes.", nameof(config.Identities));
            }

            string?[] values =
            [
                identity.Thumbprint, identity.SerialNumber, identity.CommonName, identity.Subject,
                identity.DnsName, identity.IssuerSubject, identity.IssuerThumbprint, identity.IssuerCertificateId
            ];
            if (values.Any(value => value is not null && string.IsNullOrWhiteSpace(value)))
            {
                throw new ArgumentException("Client certificate identity values must not be empty.", nameof(config.Identities));
            }
        }
    }

    private static void Fail(GatewayContext context, ValidateClientCertificateConfig config, string reason, string message)
    {
        if (config.IgnoreError == true)
        {
            ValidateJwtHandler.RecordFailure(context, "validate-client-certificate", reason, message, 403);
            context.Trace($"validate-client-certificate: {message}");
            return;
        }

        ValidateJwtHandler.WriteFailure(context, "validate-client-certificate", reason, message, 403);
        // Certificate rejection is a policy error so callers can execute their on-error section.
        throw new SecurityException(message);
    }

    private sealed class CertificateValidationFailure(string reason, string message) : SecurityException(message)
    {
        internal string Reason { get; } = reason;
    }
}
