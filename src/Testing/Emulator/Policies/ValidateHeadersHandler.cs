// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ValidateHeadersHandler : PolicyHandler<ValidateHeadersConfig>
{
    public override string PolicyName => nameof(IOutboundContext.ValidateHeaders);

    protected override void Handle(GatewayContext context, ValidateHeadersConfig config)
    {
        var specified = SchemaValidationSession.Action(config.SpecifiedHeaderAction, nameof(config.SpecifiedHeaderAction));
        var unspecified = SchemaValidationSession.Action(config.UnspecifiedHeaderAction, nameof(config.UnspecifiedHeaderAction));
        var overrides = ApiSchemaValidation.Overrides(config.Headers, header => header.Name, header => header.Action,
            StringComparer.OrdinalIgnoreCase);
        var session = new SchemaValidationSession(context, "validate-headers", config.ErrorsVariableName);
        if (specified == "ignore" && unspecified == "ignore" && overrides.Values.All(action => action == "ignore"))
        {
            session.Complete();
            return;
        }

        var metadata = ApiSchemaValidation.RequireMetadata(context, PolicyName);
        var definitions = ApiSchemaValidation.Response(metadata, context.Response.StatusCode)?.Headers ??
                          new Dictionary<string, ApiParameterValidationMetadata>();
        ApiSchemaValidation.Parameters(session, definitions, context.Response.Headers, "ResponseHeader",
            specified, unspecified, overrides, StringComparer.OrdinalIgnoreCase);
        session.Complete();
    }
}