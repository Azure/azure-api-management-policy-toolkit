// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateJwtDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-jwt";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "header-name", "HeaderName");
        context.AddOptionalStringProp(props, element, "query-parameter-name", "QueryParameterName");
        context.AddOptionalStringProp(props, element, "token-value", "TokenValue");
        context.AddOptionalIntProp(props, element, "failed-validation-httpcode", "FailedValidationHttpCode");
        context.AddOptionalStringProp(props, element, "failed-validation-error-message", "FailedValidationErrorMessage");
        context.AddOptionalBoolProp(props, element, "require-expiration-time", "RequireExpirationTime");
        context.AddOptionalStringProp(props, element, "require-scheme", "RequireScheme");
        context.AddOptionalBoolProp(props, element, "require-signed-tokens", "RequireSignedTokens");
        context.AddOptionalIntProp(props, element, "clock-skew", "ClockSkew");
        context.AddOptionalStringProp(props, element, "output-token-variable-name", "OutputTokenVariableName");

        var openIdConfigs = element.Elements("openid-config").ToList();
        if (openIdConfigs.Count > 0)
        {
            var items = openIdConfigs.Select(e =>
            {
                var url = e.Attribute("url")?.Value ?? "";
                return $"new OpenIdConfig {{ Url = {context.HandleValue(url, "OpenIdUrl")} }}";
            });
            props.Add($"OpenIdConfigs = new OpenIdConfig[] {{ {string.Join(", ", items)} }}");
        }

        EmitJwtKeys(context, element, "issuer-signing-keys", "IssuerSigningKeys", props);
        EmitJwtKeys(context, element, "decryption-keys", "DescriptionKeys", props);

        var audiences = element.Element("audiences")?.Elements("audience")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (audiences != null && audiences.Count > 0)
        {
            props.Add($"Audiences = new[] {{ {string.Join(", ", audiences.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var issuers = element.Element("issuers")?.Elements("issuer")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (issuers != null && issuers.Count > 0)
        {
            props.Add($"Issuers = new[] {{ {string.Join(", ", issuers.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var claims = element.Element("required-claims")?.Elements("claim").ToList();
        if (claims != null && claims.Count > 0)
        {
            var claimConfigs = claims.Select(c =>
            {
                var claimProps = new List<string> { $"Name = {PolicyDecompilerContext.Literal(c.Attribute("name")?.Value ?? "")}" };
                var match = c.Attribute("match")?.Value;
                if (match != null) claimProps.Add($"Match = {PolicyDecompilerContext.Literal(match)}");
                var separator = c.Attribute("separator")?.Value;
                if (separator != null) claimProps.Add($"Separator = {PolicyDecompilerContext.Literal(separator)}");
                var values = c.Elements("value").Select(v => PolicyDecompilerContext.GetElementText(v)).ToList();
                if (values.Count > 0)
                {
                    claimProps.Add($"Values = new[] {{ {string.Join(", ", values.Select(PolicyDecompilerContext.Literal))} }}");
                }
                return $"new ClaimConfig {{ {string.Join(", ", claimProps)} }}";
            });
            props.Add($"RequiredClaims = new ClaimConfig[] {{ {string.Join(", ", claimConfigs)} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateJwt", "ValidateJwtConfig", props);
    }

    private static void EmitJwtKeys(PolicyDecompilerContext context, XElement element, string xmlElementName, string configPropertyName, List<string> props)
    {
        var keysElement = element.Element(xmlElementName);
        if (keysElement == null) return;

        var keys = keysElement.Elements("key").ToList();
        if (keys.Count == 0) return;

        var keyConfigs = keys.Select(k =>
        {
            var certId = k.Attribute("certificate-id")?.Value;
            if (certId != null)
            {
                var kProps = new List<string> { $"CertificateId = {PolicyDecompilerContext.Literal(certId)}" };
                var id = k.Attribute("id")?.Value;
                if (id != null) kProps.Add($"Id = {PolicyDecompilerContext.Literal(id)}");
                return $"new CertificateKeyConfig {{ {string.Join(", ", kProps)} }}";
            }

            var n = k.Attribute("n")?.Value;
            var e = k.Attribute("e")?.Value;
            if (n != null && e != null)
            {
                var kProps = new List<string> { $"Modulus = {PolicyDecompilerContext.Literal(n)}", $"Exponent = {PolicyDecompilerContext.Literal(e)}" };
                var id = k.Attribute("id")?.Value;
                if (id != null) kProps.Add($"Id = {PolicyDecompilerContext.Literal(id)}");
                return $"new AsymmetricKeyConfig {{ {string.Join(", ", kProps)} }}";
            }

            var value = PolicyDecompilerContext.GetElementText(k);
            if (!string.IsNullOrEmpty(value))
            {
                var kProps = new List<string> { $"Value = {PolicyDecompilerContext.Literal(value)}" };
                var id = k.Attribute("id")?.Value;
                if (id != null) kProps.Add($"Id = {PolicyDecompilerContext.Literal(id)}");
                return $"new Base64KeyConfig {{ {string.Join(", ", kProps)} }}";
            }

            return "new Base64KeyConfig { Value = \"\" }";
        });

        props.Add($"{configPropertyName} = new KeyConfig[] {{ {string.Join(", ", keyConfigs)} }}");
    }
}
