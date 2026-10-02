// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateAzureAdTokenHandler : PolicyHandler<ValidateAzureAdTokenConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateAzureAdToken);

    protected override void Handle(GatewayContext context, ValidateAzureAdTokenConfig config)
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
            ValidateJwtHandler.RejectToken(context, "validate-azure-ad-token", config.FailedValidationHttpCode,
                config.FailedValidationErrorMessage, error);
        }
    }
}
