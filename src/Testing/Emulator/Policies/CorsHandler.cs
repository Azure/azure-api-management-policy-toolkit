// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class CorsHandler : PolicyHandler<CorsConfig>
{
    public override string PolicyName => nameof(IInboundContext.Cors);

    protected override void Handle(GatewayContext context, CorsConfig config)
    {
        var origin = context.Request.Headers.TryGetValue("Origin", out var originValues)
            ? originValues.FirstOrDefault()
            : null;

        string? allowedOrigin = null;
        if (config.AllowedOrigins.Contains("*"))
        {
            allowedOrigin = "*";
        }
        else if (origin is not null)
        {
            foreach (var allowed in config.AllowedOrigins)
            {
                if (string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase))
                {
                    allowedOrigin = origin;
                    break;
                }
            }
        }

        if (allowedOrigin is not null)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = [allowedOrigin];
        }

        if (config.AllowCredentials == true)
        {
            context.Response.Headers["Access-Control-Allow-Credentials"] = ["true"];
        }

        if (config.AllowedMethods is not null)
        {
            context.Response.Headers["Access-Control-Allow-Methods"] =
                [string.Join(",", config.AllowedMethods)];
        }

        if (config.AllowedHeaders.Length > 0)
        {
            context.Response.Headers["Access-Control-Allow-Headers"] =
                [string.Join(",", config.AllowedHeaders)];
        }

        if (config.ExposeHeaders is not null && config.ExposeHeaders.Length > 0)
        {
            context.Response.Headers["Access-Control-Expose-Headers"] =
                [string.Join(",", config.ExposeHeaders)];
        }

        if (config.PreflightResultMaxAge is not null)
        {
            context.Response.Headers["Access-Control-Max-Age"] =
                [config.PreflightResultMaxAge.Value.ToString()];
        }
    }
}