// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOnErrorContext))
]
internal class TraceHandler : PolicyHandler<TraceConfig>
{
    public override string PolicyName => nameof(IInboundContext.Trace);

    protected override void Handle(GatewayContext context, TraceConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Source, nameof(config.Source));
        ArgumentNullException.ThrowIfNull(config.Message, nameof(config.Message));

        var severity = config.Severity ?? "verbose";
        if (severity is not ("verbose" or "information" or "error"))
        {
            throw new ArgumentException("Trace severity must be verbose, information, or error.", nameof(config.Severity));
        }

        var metadata = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var item in config.Metadata ?? [])
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name) || item.Value is null)
            {
                throw new ArgumentException(
                    "Trace metadata entries must have a non-empty Name and non-null Value.", nameof(config.Metadata));
            }

            if (!metadata.TryAdd(item.Name, item.Value))
            {
                throw new ArgumentException($"Trace metadata name '{item.Name}' must be unique.", nameof(config.Metadata));
            }
        }

        context.LoggerStore.TracesInternal.Add(new TraceEvent(
            config.Source, config.Message, severity, metadata.ToImmutable()));
    }
}