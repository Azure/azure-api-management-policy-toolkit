// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateClientCertificateDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-client-certificate";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddOptionalBoolProp(props, element, "validate-revocation", "ValidateRevocation");
        context.AddOptionalBoolProp(props, element, "validate-trust", "ValidateTrust");
        context.AddOptionalBoolProp(props, element, "validate-not-before", "ValidateNotBefore");
        context.AddOptionalBoolProp(props, element, "validate-not-after", "ValidateNotAfter");
        context.AddOptionalBoolProp(props, element, "ignore-error", "IgnoreError");

        var identities = element.Element("identities")?.Elements("identity").ToList();
        if (identities != null && identities.Count > 0)
        {
            var identityConfigs = identities.Select(i =>
            {
                var idProps = new List<string>();
                var thumbprint = i.Attribute("thumbprint")?.Value;
                if (thumbprint != null)
                {
                    idProps.Add($"Thumbprint = {PolicyDecompilerContext.Literal(thumbprint)}");
                }

                var serialNumber = i.Attribute("serial-number")?.Value;
                if (serialNumber != null)
                {
                    idProps.Add($"SerialNumber = {PolicyDecompilerContext.Literal(serialNumber)}");
                }

                var commonName = i.Attribute("common-name")?.Value;
                if (commonName != null)
                {
                    idProps.Add($"CommonName = {PolicyDecompilerContext.Literal(commonName)}");
                }

                var subject = i.Attribute("subject")?.Value;
                if (subject != null)
                {
                    idProps.Add($"Subject = {PolicyDecompilerContext.Literal(subject)}");
                }

                var dnsName = i.Attribute("dns-name")?.Value;
                if (dnsName != null)
                {
                    idProps.Add($"DnsName = {PolicyDecompilerContext.Literal(dnsName)}");
                }

                var issuerSubject = i.Attribute("issuer-subject")?.Value;
                if (issuerSubject != null)
                {
                    idProps.Add($"IssuerSubject = {PolicyDecompilerContext.Literal(issuerSubject)}");
                }

                var issuerThumbprint = i.Attribute("issuer-thumbprint")?.Value;
                if (issuerThumbprint != null)
                {
                    idProps.Add($"IssuerThumbprint = {PolicyDecompilerContext.Literal(issuerThumbprint)}");
                }

                var issuerCertificateId = i.Attribute("issuer-certificate-id")?.Value;
                if (issuerCertificateId != null)
                {
                    idProps.Add($"IssuerCertificateId = {PolicyDecompilerContext.Literal(issuerCertificateId)}");
                }

                return $"new CertificateIdentity {{ {string.Join(", ", idProps)} }}";
            });
            props.Add($"Identities = new CertificateIdentity[] {{ {string.Join(", ", identityConfigs)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateClientCertificate",
            "ValidateClientCertificateConfig", props);
    }
}
