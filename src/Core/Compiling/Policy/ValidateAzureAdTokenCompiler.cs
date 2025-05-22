// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateAzureAdTokenCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateAzureAdToken);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateAzureAdTokenConfig>(context, "validate-azure-ad-token",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("validate-azure-ad-token");

        if (!element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.TenantId), "tenant-id"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "validate-azure-ad-token",
                nameof(ValidateAzureAdTokenConfig.TenantId)
            ));
            return;
        }

        element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.HeaderName), "header-name");
        element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.QueryParameterName), "query-parameter-name");
        element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.TokenValue), "token-value");
        element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.FailedValidationHttpCode),
            "failed-validation-httpcode");
        element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.FailedValidationErrorMessage),
            "failed-validation-error-message");
        element.AddAttribute(values, nameof(ValidateAzureAdTokenConfig.OutputTokenVariableName),
            "output-token-variable-name");

        GenericCompiler.HandleList(element, values, nameof(ValidateAzureAdTokenConfig.BackendApplicationIds),
            "backend-application-ids", "application-id");
        GenericCompiler.HandleList(element, values, nameof(ValidateAzureAdTokenConfig.ClientApplicationIds),
            "client-application-ids",
            "application-id");
        GenericCompiler.HandleList(element, values, nameof(ValidateAzureAdTokenConfig.Audiences), "audiences",
            "audience");

        if (values.TryGetValue(nameof(ValidateAzureAdTokenConfig.RequiredClaims), out InitializerValue? requiredClaims))
        {
            element.Add(ClaimsConfigCompiler.HandleRequiredClaims(context, requiredClaims));
        }

        if (values.TryGetValue(nameof(ValidateAzureAdTokenConfig.DecryptionKeys), out InitializerValue? decryptionKeys))
        {
            element.Add(HandleDecryptionKeys(context, decryptionKeys));
        }

        context.AddPolicy(element);
    }

    private static XElement HandleDecryptionKeys(ICompilationContext context, InitializerValue decryptionKeys)
    {
        XElement listElement = new("decryption-keys");
        foreach (InitializerValue initializer in decryptionKeys.UnnamedValues ?? [])
        {
            if (!initializer.TryGetValues<DecryptionKey>(
                    out IReadOnlyDictionary<string, InitializerValue>? decryptionKey))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotAnObjectCreation,
                    initializer.Node.GetLocation(),
                    "validate-azure-ad-token.decryption-keys.key",
                    nameof(DecryptionKey)
                ));
            }

            XElement decryptionElement = new("key");
            if (!decryptionElement.AddAttribute(decryptionKey, nameof(DecryptionKey.CertificateId),
                    "certificate-id"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    initializer.Node.GetLocation(),
                    "validate-azure-ad-token.decryption-keys.key",
                    nameof(DecryptionKey.CertificateId)
                ));
            }

            listElement.Add(decryptionElement);
        }

        return listElement;
    }
}