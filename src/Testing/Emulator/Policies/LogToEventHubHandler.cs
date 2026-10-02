// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class LogToEventHubHandler : PolicyHandler<LogToEventHubConfig>
{
    const int MaxMessageBytes = 204000;

    public override string PolicyName => nameof(IInboundContext.LogToEventHub);

    protected override void Handle(GatewayContext context, LogToEventHubConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config.LoggerId, nameof(config.LoggerId));
        ArgumentNullException.ThrowIfNull(config.Value, nameof(config.Value));

        if (!context.LoggerStore.TryGet(config.LoggerId, out var logger))
        {
            throw new InvalidOperationException(
                $"Logger '{config.LoggerId}' is not configured. Use SetupLoggerStore().Add(loggerId) before running the policy.");
        }

        var content = new byte[Math.Min(Encoding.UTF8.GetByteCount(config.Value), MaxMessageBytes)];
        // A bounded encoder writes only complete UTF-8 characters when the buffer fills.
        Encoding.UTF8.GetEncoder().Convert(
            config.Value.AsSpan(), content.AsSpan(), true, out _, out var bytesUsed, out _);

        var hubEvent = new EventHubEvent(Encoding.UTF8.GetString(content, 0, bytesUsed), config.PartitionId, config.PartitionKey);
        logger.EventsInternal.Add(hubEvent);
    }
}