// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateParametersHandler : PolicyHandler<ValidateParametersConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateParameters);

    protected override void Handle(GatewayContext context, ValidateParametersConfig config)
    {
        var specified = SchemaValidationSession.Action(config.SpecifiedParameterAction, nameof(config.SpecifiedParameterAction));
        var unspecified = SchemaValidationSession.Action(config.UnspecifiedParameterAction, nameof(config.UnspecifiedParameterAction));
        var headerSpecified = SchemaValidationSession.Action(
            config.Headers is null ? specified : config.Headers.SpecifiedParameterAction, "Headers.SpecifiedParameterAction");
        var headerUnspecified = SchemaValidationSession.Action(
            config.Headers is null ? unspecified : config.Headers.UnspecifiedParameterAction, "Headers.UnspecifiedParameterAction");
        var querySpecified = SchemaValidationSession.Action(
            config.Query is null ? specified : config.Query.SpecifiedParameterAction, "Query.SpecifiedParameterAction");
        var queryUnspecified = SchemaValidationSession.Action(
            config.Query is null ? unspecified : config.Query.UnspecifiedParameterAction, "Query.UnspecifiedParameterAction");
        var pathSpecified = SchemaValidationSession.Action(
            config.Path is null ? specified : config.Path.SpecifiedParameterAction, "Path.SpecifiedParameterAction");
        var headerOverrides = ApiSchemaValidation.Overrides(config.Headers?.Parameters, parameter => parameter.Name,
            parameter => parameter.Action, StringComparer.OrdinalIgnoreCase);
        var queryOverrides = ApiSchemaValidation.Overrides(config.Query?.Parameters, parameter => parameter.Name,
            parameter => parameter.Action, StringComparer.Ordinal);
        var pathOverrides = ApiSchemaValidation.Overrides(config.Path?.Parameters, parameter => parameter.Name,
            parameter => parameter.Action, StringComparer.Ordinal);
        var session = new SchemaValidationSession(context, "validate-parameters", config.ErrorsVariableName);
        var actions = new[] { headerSpecified, headerUnspecified, querySpecified, queryUnspecified, pathSpecified, unspecified };
        if (actions.Concat(headerOverrides.Values).Concat(queryOverrides.Values).Concat(pathOverrides.Values)
            .All(action => action == "ignore"))
        {
            session.Complete();
            return;
        }

        var metadata = ApiSchemaValidation.RequireMetadata(context, PolicyName);
        ApiSchemaValidation.Parameters(session, metadata.RequestHeaders, context.Request.Headers, "RequestHeader",
            headerSpecified, headerUnspecified, headerOverrides, StringComparer.OrdinalIgnoreCase);
        ApiSchemaValidation.Parameters(session, metadata.QueryParameters, context.Request.Url.Query, "QueryParameter",
            querySpecified, queryUnspecified, queryOverrides, StringComparer.Ordinal);
        ApiSchemaValidation.Parameters(session, metadata.PathParameters,
            context.Request.MatchedParameters.Select(parameter =>
                new KeyValuePair<string, string[]>(parameter.Key, [parameter.Value])), "PathParameter",
            pathSpecified, unspecified, pathOverrides, StringComparer.Ordinal);
        session.Complete();
    }
}