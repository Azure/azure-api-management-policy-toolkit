// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class AuthenticationBasicHandler : PolicyHandler<string, string>
{
    public override string PolicyName => nameof(IInboundContext.AuthenticationBasic);

    protected override void Handle(GatewayContext context, string username, string password)
    {
        var authHeader = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";
        context.Request.Headers["Authorization"] = [authHeader];
    }
}