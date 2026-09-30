// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
[Section(nameof(IOutboundContext))]
[Section(nameof(IBackendContext))]
internal class GetAuthorizationContextHandler : PolicyHandler<GetAuthorizationContextConfig>
{
    public List<Tuple<
        Func<GatewayContext, GetAuthorizationContextConfig, bool>,
        Func<AuthorizationRequest, Authorization>
    >> ProvideAuthorizationHooks { get; } = [];

    public override string PolicyName => nameof(IInboundContext.GetAuthorizationContext);

    protected override void Handle(GatewayContext context, GetAuthorizationContextConfig config)
    {
        ValidateRequiredValue(config.ProviderId, nameof(config.ProviderId));
        ValidateRequiredValue(config.AuthorizationId, nameof(config.AuthorizationId));
        ValidateRequiredValue(config.ContextVariableName, nameof(config.ContextVariableName));

        var identityType = config.IdentityType ?? "managed";
        if (identityType is not ("managed" or "jwt"))
        {
            throw new ArgumentException(
                "The authorization identity type must be managed or jwt.", nameof(config.IdentityType));
        }

        if (identityType == "jwt")
        {
            ValidateRequiredValue(config.Identity, nameof(config.Identity));
        }

        var request = new AuthorizationRequest(
            config.ProviderId, config.AuthorizationId, identityType, identityType == "jwt" ? config.Identity : null);
        var hook = ProvideAuthorizationHooks.Find(value => value.Item1(context, config))?.Item2;

        Authorization authorization;
        try
        {
            if (hook is not null)
            {
                authorization = hook(request);
            }
            else
            {
                var provider = context.Services.Resolve<IAuthorizationProvider>(config.ProviderId)
                    ?? context.Services.Resolve<IAuthorizationProvider>()
                    ?? throw new InvalidOperationException(
                        "No IAuthorizationProvider registered. Register one via " +
                        "test.Context.Services.Register<IAuthorizationProvider>(provider) " +
                        "or configure GetAuthorizationContext().ReturnsAuthorization(...).");
                var acquisition = provider.GetAuthorizationAsync(request)
                    ?? throw new InvalidOperationException("The authorization provider returned no acquisition task.");
                authorization = acquisition.GetAwaiter().GetResult();
            }

            if (authorization is null || string.IsNullOrWhiteSpace(authorization.AccessToken) || authorization.Claims is null)
            {
                throw new InvalidOperationException(
                    "The authorization provider must return an Authorization with a nonempty access token and nonnull claims.");
            }
        }
        catch (Exception error) when (error is HttpRequestException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or TimeoutException or OperationCanceledException
            or SecurityTokenException)
        {
            if (config.IgnoreError == true)
            {
                context.Variables[config.ContextVariableName] = null!;
                context.Trace($"{PolicyName}: authorization acquisition failed.");
                return;
            }

            context.Response.StatusCode = 500;
            context.Response.StatusReason = "Internal Server Error";
            context.LastError.Source = "get-authorization-context";
            context.LastError.Reason = "AuthorizationAcquisitionFailed";
            context.LastError.Message = error.Message;
            context.LastError.HttpErrorCode = 500;
            throw;
        }

        context.Variables[config.ContextVariableName] = authorization;
    }

    private static void ValidateRequiredValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The authorization policy value must not be empty.", parameterName);
        }
    }
}
