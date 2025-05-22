// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateClientCertificateCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateClientCertificate);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateClientCertificateConfig>(context, "validate-client-certificate",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("validate-client-certificate");

        element.AddAttribute(values, nameof(ValidateClientCertificateConfig.ValidateRevocation), "validate-revocation");
        element.AddAttribute(values, nameof(ValidateClientCertificateConfig.ValidateTrust), "validate-trust");
        element.AddAttribute(values, nameof(ValidateClientCertificateConfig.ValidateNotBefore), "validate-not-before");
        element.AddAttribute(values, nameof(ValidateClientCertificateConfig.ValidateNotAfter), "validate-not-after");
        element.AddAttribute(values, nameof(ValidateClientCertificateConfig.IgnoreError), "ignore-error");

        if (values.TryGetValue(nameof(ValidateClientCertificateConfig.Identities),
                out InitializerValue? identitiesValue))
        {
            XElement identities = HandleIdentities(context, identitiesValue);
            element.Add(identities);
        }

        context.AddPolicy(element);
    }

    private static XElement HandleIdentities(ICompilationContext context, InitializerValue identitiesValue)
    {
        XElement identities = new("identities");
        foreach (InitializerValue identityValue in identitiesValue.UnnamedValues ?? [])
        {
            if (!identityValue.TryGetValues<CertificateIdentity>(
                    out IReadOnlyDictionary<string, InitializerValue>? certValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    identityValue.Node.GetLocation(),
                    "identity",
                    nameof(CertificateIdentity)
                ));
                continue;
            }

            XElement identity = new("identity");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.Thumbprint), "thumbprint");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.SerialNumber), "serial-number");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.CommonName), "common-name");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.Subject), "subject");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.DnsName), "dns-name");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.IssuerSubject), "issuer-subject");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.IssuerThumbprint), "issuer-thumbprint");
            identity.AddAttribute(certValues, nameof(CertificateIdentity.IssuerCertificateId), "issuer-certificate-id");
            identities.Add(identity);
        }

        return identities;
    }
}