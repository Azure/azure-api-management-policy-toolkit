// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class ValidateOdataRequestHandler : PolicyHandler<ValidateOdataRequestConfig>
{
    public override string PolicyName => nameof(IInboundContext.ValidateOdataRequest);

    protected override void Handle(GatewayContext context, ValidateOdataRequestConfig config)
    {
        var defaultVersion = ConfigurationVersion(config.DefaultOdataVersion ?? "4.0", nameof(config.DefaultOdataVersion));
        var min = config.MinOdataVersion is null ? (decimal?)null :
            ConfigurationVersion(config.MinOdataVersion, nameof(config.MinOdataVersion));
        var max = config.MaxOdataVersion is null ? (decimal?)null :
            ConfigurationVersion(config.MaxOdataVersion, nameof(config.MaxOdataVersion));
        if (min > max) throw new ArgumentException("MinOdataVersion cannot exceed MaxOdataVersion.");
        if (config.MaxSize is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.MaxSize), "OData maximum payload size cannot be negative.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Request.Method);
        var session = new SchemaValidationSession(context, "validate-odata-request", config.ErrorVariableName);
        if (config.MaxSize is not null) session.CheckSize(config.MaxSize.Value, "prevent");
        SchemaValidationSession.EnsureUtf8(context.Request);

        var version = config.DefaultOdataVersion ?? "4.0";
        var versions = SchemaValidationSession.HeaderValues(context.Request.Headers, "OData-Version");
        if (versions.Length > 1 || versions.Length == 1 && !SupportedVersion(versions[0]))
        {
            session.Add("OData-Version", "ODataRequest", "ODataVersion",
                "OData-Version must contain exactly one supported version: 4.0 or 4.01.", "prevent");
        }
        if (versions.Length == 1) version = versions[0];
        var actualVersion = versions.Length == 0 ? defaultVersion :
            decimal.Parse(version, CultureInfo.InvariantCulture);
        if (min is not null && actualVersion < min || max is not null && actualVersion > max)
        {
            session.Add("OData-Version", "ODataRequest", "ODataVersion",
                $"OData version '{version}' is outside the configured version range.", "prevent");
        }
        var maxVersions = SchemaValidationSession.HeaderValues(context.Request.Headers, "OData-MaxVersion");
        if (maxVersions.Length > 1 || maxVersions.Length == 1 &&
            (!SupportedVersion(maxVersions[0]) ||
             decimal.Parse(maxVersions[0], CultureInfo.InvariantCulture) < actualVersion))
        {
            session.Add("OData-MaxVersion", "ODataRequest", "ODataVersion",
                "OData-MaxVersion must be a single supported version not less than OData-Version.", "prevent");
        }

        var request = new OdataValidationRequest(
            context.Request.Url.ToUri(), context.Request.Method, version,
            Snapshot(context.Request.Headers, StringComparer.OrdinalIgnoreCase),
            Snapshot(context.Request.Url.Query, StringComparer.Ordinal),
            context.Request.Body.Content ?? string.Empty);
        var validator = context.Services.Resolve<IOdataRequestValidator>();
        var errors = validator is not null ? validator.Validate(request) :
            OdataRequestValidation.Validate(request,
                context.Services.Resolve<OdataValidationMetadata>() ?? throw new NotSupportedException(
                    "ValidateOdataRequest requires explicit OdataValidationMetadata or IOdataRequestValidator. " +
                    "Register the API's model with test.Context.Services; the emulator does not infer EDM metadata."));
        if (errors is null || errors.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("The OData validator returned null or an empty validation error detail.");
        }
        session.AddErrors(string.Empty, "ODataRequest", "IncorrectMessage", errors, "prevent");
        session.Complete();
    }

    private static bool SupportedVersion(string version) => version is "4.0" or "4.01";

    private static decimal ConfigurationVersion(string version, string parameter)
    {
        if (!SupportedVersion(version))
        {
            throw new ArgumentException("The emulator supports OData versions 4.0 and 4.01.", parameter);
        }
        return decimal.Parse(version, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyDictionary<string, string[]> Snapshot(
        IEnumerable<KeyValuePair<string, string[]>> values, StringComparer comparer) =>
        new ReadOnlyDictionary<string, string[]>(ApiSchemaValidation.Values(values, comparer)
            .ToDictionary(item => item.Key, item => item.Value.ToArray(), comparer));
}