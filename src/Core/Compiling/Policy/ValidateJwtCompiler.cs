// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateJwtCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateJwt);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateJwtConfig>(context, "validate-jwt", out var values))
        {
            return;
        }

        var element = new XElement("validate-jwt");

        if (new[]
            {
                element.AddAttribute(values, nameof(ValidateJwtConfig.HeaderName), "header-name"),
                element.AddAttribute(values, nameof(ValidateJwtConfig.QueryParameterName), "query-parameter-name"),
                element.AddAttribute(values, nameof(ValidateJwtConfig.TokenValue), "token-value"),
            }.Count(b => b) != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OnlyOneOfTreeShouldBeDefined,
                node.ArgumentList.GetLocation(),
                "validate-jwt",
                nameof(ValidateJwtConfig.HeaderName),
                nameof(ValidateJwtConfig.QueryParameterName),
                nameof(ValidateJwtConfig.TokenValue)
            ));
            return;
        }

        element.AddAttribute(values, nameof(ValidateJwtConfig.FailedValidationHttpCode), "failed-validation-httpcode");
        element.AddAttribute(values, nameof(ValidateJwtConfig.FailedValidationErrorMessage),
            "failed-validation-error-message");
        element.AddAttribute(values, nameof(ValidateJwtConfig.RequireExpirationTime), "require-expiration-time");
        element.AddAttribute(values, nameof(ValidateJwtConfig.RequireScheme), "require-scheme");
        element.AddAttribute(values, nameof(ValidateJwtConfig.RequireSignedTokens), "require-signed-tokens");
        element.AddAttribute(values, nameof(ValidateJwtConfig.ClockSkew), "clock-skew");
        element.AddAttribute(values, nameof(ValidateJwtConfig.OutputTokenVariableName), "output-token-variable-name");

        if (values.TryGetValue(nameof(ValidateJwtConfig.OpenIdConfigs), out var openIdConfigs))
        {
            var openIdElements = HandleOpenIdConfigs(context, openIdConfigs);
            element.Add(openIdElements);
        }

        HandleKeys(context, element, values, nameof(ValidateJwtConfig.IssuerSigningKeys), "issuer-signing-keys");
        HandleKeys(context, element, values, nameof(ValidateJwtConfig.DescriptionKeys), "decryption-keys");
        GenericCompiler.HandleList(element, values, nameof(ValidateJwtConfig.Audiences), "audiences", "audience");
        GenericCompiler.HandleList(element, values, nameof(ValidateJwtConfig.Issuers), "issuers", "issuer");

        if (values.TryGetValue(nameof(ValidateJwtConfig.RequiredClaims), out var requiredClaims))
        {
            XElement claimsElement = ClaimsConfigCompiler.HandleRequiredClaims(context, requiredClaims);
            element.Add(claimsElement);
        }

        context.AddPolicy(element);
    }

    private static object[] HandleOpenIdConfigs(ICompilationContext context, InitializerValue openIdConfigs)
    {
        var openIdElements = new List<object>();
        foreach (var openIdConfig in openIdConfigs.UnnamedValues ?? [])
        {
            if (!openIdConfig.TryGetValues<OpenIdConfig>(out var openIdConfigValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    openIdConfig.Node.GetLocation(),
                    "openid-config",
                    nameof(OpenIdConfig)
                ));
                continue;
            }

            var openIdElement = new XElement("openid-config");
            if (!openIdElement.AddAttribute(openIdConfigValues, nameof(OpenIdConfig.Url), "url"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    openIdConfig.Node.GetLocation(),
                    "openid-config",
                    nameof(OpenIdConfig.Url)
                ));
                continue;
            }

            openIdElements.Add(openIdElement);
        }

        return openIdElements.ToArray();
    }

    private static void HandleKeys(
        ICompilationContext context,
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        string key,
        string listName)
    {
        if (!values.TryGetValue(key, out var listInitializer))
        {
            return;
        }

        var listElement = new XElement(listName);
        foreach (var initializer in listInitializer.UnnamedValues ?? [])
        {
            var keyValues = initializer.NamedValues;
            if (keyValues is null)
            {
                continue;
            }

            var keyElement = new XElement("key");
            keyElement.AddAttribute(keyValues, nameof(KeyConfig.Id), "id");
            switch (initializer.Type)
            {
                case nameof(Base64KeyConfig):
                    if (!keyValues.TryGetValue(nameof(Base64KeyConfig.Value), out var value))
                    {
                        context.Report(Diagnostic.Create(
                            CompilationErrors.RequiredParameterNotDefined,
                            initializer.Node.GetLocation(),
                            "key",
                            nameof(Base64KeyConfig.Value)
                        ));
                        continue;
                    }

                    keyElement.Value = value.Value!;
                    break;
                case nameof(CertificateKeyConfig):
                    if (!keyElement.AddAttribute(keyValues, nameof(CertificateKeyConfig.CertificateId),
                            "certificate-id"))
                    {
                        context.Report(Diagnostic.Create(
                            CompilationErrors.RequiredParameterNotDefined,
                            initializer.Node.GetLocation(),
                            "key",
                            nameof(CertificateKeyConfig.CertificateId)
                        ));
                        continue;
                    }

                    break;
                case nameof(AsymmetricKeyConfig):
                    if (!keyElement.AddAttribute(keyValues, nameof(AsymmetricKeyConfig.Modulus), "n"))
                    {
                        context.Report(Diagnostic.Create(
                            CompilationErrors.RequiredParameterNotDefined,
                            initializer.Node.GetLocation(),
                            "key",
                            nameof(AsymmetricKeyConfig.Modulus)
                        ));
                        continue;
                    }

                    if (!keyElement.AddAttribute(keyValues, nameof(AsymmetricKeyConfig.Exponent), "e"))
                    {
                        context.Report(Diagnostic.Create(
                            CompilationErrors.RequiredParameterNotDefined,
                            initializer.Node.GetLocation(),
                            "key",
                            nameof(AsymmetricKeyConfig.Exponent)
                        ));
                        continue;
                    }

                    break;
                default:
                    context.Report(Diagnostic.Create(
                        CompilationErrors.NotSupportedType,
                        initializer.Node.GetLocation(),
                        "key",
                        initializer.Type
                    ));
                    continue;
            }

            listElement.Add(keyElement);
        }

        element.Add(listElement);
    }
}