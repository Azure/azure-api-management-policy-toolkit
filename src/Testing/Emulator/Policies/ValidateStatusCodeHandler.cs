// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class ValidateStatusCodeHandler : PolicyHandler<ValidateStatusCodeConfig>
{
    public override string PolicyName => nameof(IOutboundContext.ValidateStatusCode);

    protected override void Handle(GatewayContext context, ValidateStatusCodeConfig config)
    {
        var action = SchemaValidationSession.Action(config.UnspecifiedStatusCodeAction, nameof(config.UnspecifiedStatusCodeAction));
        var overrides = new Dictionary<uint, string>();
        foreach (var rule in config.StatusCodes ?? [])
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (rule.Code is < 100 or > 599)
            {
                throw new ArgumentOutOfRangeException(nameof(rule.Code), rule.Code, "HTTP status codes must be between 100 and 599.");
            }
            var ruleAction = SchemaValidationSession.Action(rule.Action, nameof(rule.Action));
            if (!overrides.TryAdd(rule.Code, ruleAction))
            {
                throw new ArgumentException($"Duplicate validation override for status code {rule.Code}.");
            }
        }
        var status = context.Response.StatusCode;
        if (status is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(context.Response.StatusCode), status, "HTTP status codes must be between 100 and 599.");
        }
        if (overrides.TryGetValue((uint)status, out var actionOverride)) action = actionOverride;
        var session = new SchemaValidationSession(context, "validate-status-code", config.ErrorVariableName);
        if (action != "ignore")
        {
            var metadata = ApiSchemaValidation.RequireMetadata(context, PolicyName);
            if (ApiSchemaValidation.Response(metadata, status) is null)
            {
                session.Add(status.ToString(System.Globalization.CultureInfo.InvariantCulture), "StatusCode", "Unspecified",
                    $"Response status code {status} is not allowed by the API schema.", action);
            }
        }
        session.Complete();
    }
}