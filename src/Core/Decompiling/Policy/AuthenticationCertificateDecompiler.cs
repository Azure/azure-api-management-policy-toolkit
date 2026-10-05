// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AuthenticationCertificateDecompiler : IPolicyDecompiler
{
    public string PolicyName => "authentication-certificate";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var body = element.Attribute("body")?.Value;
        if (body != null && !context.IsExpression(body) && !PolicyDecompilerContext.ContainsNamedValueToken(body))
        {
            // The config's Body is a byte[]; a literal value has no typed representation.
            new InlinePolicyDecompiler().Decompile(writer, element, contextVar, context);
            return;
        }

        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "thumbprint", "Thumbprint");
        context.AddOptionalStringProp(props, element, "certificate-id", "CertificateId");
        context.AddOptionalProp(props, element, "body", "Body", "byte[]");
        context.AddOptionalStringProp(props, element, "password", "Password");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "AuthenticationCertificate", "CertificateAuthenticationConfig", props);
    }
}
