// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ValidateAzureAdTokenDecompiler : IPolicyDecompiler
{
    public string PolicyName => "validate-azure-ad-token";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        context.AddRequiredExprStringProp(props, element, "tenant-id", "TenantId");
        context.AddOptionalStringProp(props, element, "header-name", "HeaderName");
        context.AddOptionalStringProp(props, element, "query-parameter-name", "QueryParameterName");
        context.AddOptionalStringProp(props, element, "token-value", "TokenValue");
        context.AddOptionalIntProp(props, element, "failed-validation-httpcode", "FailedValidationHttpCode");
        context.AddOptionalStringProp(props, element, "failed-validation-error-message",
            "FailedValidationErrorMessage");
        context.AddOptionalStringProp(props, element, "output-token-variable-name", "OutputTokenVariableName");

        var backendAppIds = element.Element("backend-application-ids")?.Elements("application-id")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (backendAppIds != null && backendAppIds.Count > 0)
        {
            props.Add(
                $"BackendApplicationIds = new[] {{ {string.Join(", ", backendAppIds.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var clientAppIds = element.Element("client-application-ids")?.Elements("application-id")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (clientAppIds != null && clientAppIds.Count > 0)
        {
            props.Add(
                $"ClientApplicationIds = new[] {{ {string.Join(", ", clientAppIds.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var audiences = element.Element("audiences")?.Elements("audience")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (audiences != null && audiences.Count > 0)
        {
            props.Add(
                $"Audiences = new[] {{ {string.Join(", ", audiences.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var claims = element.Element("required-claims")?.Elements("claim").ToList();
        if (claims != null && claims.Count > 0)
        {
            var claimConfigs = claims.Select(c =>
            {
                var claimProps = new List<string>
                {
                    $"Name = {PolicyDecompilerContext.Literal(c.Attribute("name")?.Value ?? "")}"
                };
                var match = c.Attribute("match")?.Value;
                if (match != null)
                {
                    claimProps.Add($"Match = {PolicyDecompilerContext.Literal(match)}");
                }

                var separator = c.Attribute("separator")?.Value;
                if (separator != null)
                {
                    claimProps.Add($"Separator = {PolicyDecompilerContext.Literal(separator)}");
                }

                var values = c.Elements("value").Select(v => PolicyDecompilerContext.GetElementText(v)).ToList();
                if (values.Count > 0)
                {
                    claimProps.Add(
                        $"Values = new[] {{ {string.Join(", ", values.Select(PolicyDecompilerContext.Literal))} }}");
                }

                return $"new ClaimConfig {{ {string.Join(", ", claimProps)} }}";
            });
            props.Add($"RequiredClaims = new ClaimConfig[] {{ {string.Join(", ", claimConfigs)} }}");
        }

        var decryptionKeysElement = element.Element("decryption-keys");
        if (decryptionKeysElement != null)
        {
            var keys = decryptionKeysElement.Elements("key").ToList();
            if (keys.Count > 0)
            {
                var keyConfigs = keys.Select(k =>
                {
                    var certId = k.Attribute("certificate-id")?.Value ?? "";
                    return $"new DecryptionKey {{ CertificateId = {PolicyDecompilerContext.Literal(certId)} }}";
                });
                props.Add($"DecryptionKeys = new DecryptionKey[] {{ {string.Join(", ", keyConfigs)} }}");
            }
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "ValidateAzureAdToken", "ValidateAzureAdTokenConfig",
            props);
    }
}
