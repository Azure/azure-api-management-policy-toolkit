// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateJwtHandler : PolicyHandler<ValidateJwtConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateJwt);

    protected override void Handle(GatewayContext context, ValidateJwtConfig config)
    {
        if (config.OutputTokenVariableName is not null)
        {
            context.Variables.Remove(config.OutputTokenVariableName);
        }

        try
        {
            var token = IdentityTokenValidator.Validate(context, config);
            if (config.OutputTokenVariableName is not null)
            {
                context.Variables[config.OutputTokenVariableName] = token;
            }
        }
        catch (SecurityTokenException error)
        {
            RejectToken(context, "validate-jwt", config.FailedValidationHttpCode,
                config.FailedValidationErrorMessage, error);
        }
    }

    internal static void RejectToken(GatewayContext context, string source, int? statusCode,
        string? configuredMessage, SecurityTokenException error)
    {
        var (reason, message) = error switch
        {
            IdentityValidationException failure => (failure.Reason, failure.Message),
            SecurityTokenExpiredException => ("TokenExpired", "JWT has expired."),
            SecurityTokenNotYetValidException => ("TokenNotYetValid", "JWT is not yet valid."),
            SecurityTokenNoExpirationException => ("TokenExpirationMissing", "JWT expiration time is required."),
            SecurityTokenInvalidIssuerException => ("TokenIssuerNotAllowed", "JWT issuer is not allowed."),
            SecurityTokenInvalidAudienceException => ("TokenAudienceNotAllowed", "JWT audience is not allowed."),
            SecurityTokenInvalidSignatureException or SecurityTokenSignatureKeyNotFoundException =>
                ("TokenSignatureInvalid", "JWT signature is invalid."),
            SecurityTokenInvalidAlgorithmException => ("TokenSignatureInvalid", "JWT signing algorithm is not allowed."),
            SecurityTokenDecryptionFailedException => ("TokenDecryptionFailed", "JWT decryption failed."),
            _ => ("TokenValidationFailed", "JWT validation failed.")
        };
        WriteFailure(context, source, reason, configuredMessage ?? message, statusCode ?? 401);
        throw new FinishSectionProcessingException();
    }

    internal static void RecordFailure(GatewayContext context, string source, string reason, string message, int code)
    {
        context.LastError.Source = source;
        context.LastError.Reason = reason;
        context.LastError.Message = message;
        context.LastError.Section = "inbound";
        context.LastError.HttpErrorCode = code;
    }

    internal static void WriteFailure(GatewayContext context, string source, string reason, string message, int code)
    {
        RecordFailure(context, source, reason, message, code);
        using var response = new HttpResponseMessage((HttpStatusCode)code);
        ResponseUtilities.Overwrite(context.Response, code, response.ReasonPhrase ?? string.Empty);
        context.Response.Headers["Content-Type"] = ["application/json"];
        context.Response.Body.Content = JsonSerializer.Serialize(new { statusCode = code, message });
        context.ResponseTerminated = true;
    }
}
