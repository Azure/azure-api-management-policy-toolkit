// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IdentityModel.Tokens.Jwt;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext)), Section(nameof(IOutboundContext))]
internal class AuthenticationManagedIdentityHandler : PolicyHandler<ManagedIdentityAuthenticationConfig>
{
    public List<Tuple<
        Func<GatewayContext, ManagedIdentityAuthenticationConfig, bool>,
        Func<string, string?, string>
    >> ProvideTokenHooks { get; } = new();

    public override string PolicyName => nameof(IInboundContext.AuthenticationManagedIdentity);

    protected override void Handle(GatewayContext context, ManagedIdentityAuthenticationConfig config)
    {
        var provideTokenHook = ProvideTokenHooks.FirstOrDefault(hook => hook.Item1(context, config));
        var hook = provideTokenHook is not null ? provideTokenHook.Item2 : DefaultTokenProvider;
        var token = CreateTokenByHook(hook, config);

        if (!string.IsNullOrWhiteSpace(config.OutputTokenVariableName))
        {
            context.Variables[config.OutputTokenVariableName] = token;
        }
        else
        {
            context.Request.Headers["Authorization"] = [$"Bearer {token}"];
        }
    }

    private string CreateTokenByHook(Func<string, string?, string> tokenProvider,
        ManagedIdentityAuthenticationConfig config)
    {
        string token;
        try
        {
            token = tokenProvider(config.Resource, config.ClientId);
        }
        catch
        {
            if (config.IgnoreError ?? false)
            {
                token = "";
            }
            else
            {
                throw;
            }
        }

        return token;
    }

    private string DefaultTokenProvider(string resourceId, string? clientId)
    {
        byte[] securityKey = JwtTokenUtilities.GenerateKeyBytes(256);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(securityKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().CreateJwtSecurityToken(
            issuer: !string.IsNullOrWhiteSpace(clientId) ? clientId : "system-assigned",
            audience: resourceId,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}