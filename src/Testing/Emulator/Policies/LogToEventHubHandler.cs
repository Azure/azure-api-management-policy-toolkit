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
        if (!context.LoggerStore.TryGet(config.LoggerId, out var logger))
        {
            return;
        }

        var content = Encoding.UTF8.GetBytes(config.Value);
        if (content.Length > MaxMessageBytes)
        {
            var copyBytes = content;
            content = new byte[MaxMessageBytes];
            Array.Copy(copyBytes, content, MaxMessageBytes);
        }

        var hubEvent = new EventHubEvent(Encoding.UTF8.GetString(content), config.PartitionId, config.PartitionKey);
        logger.EventsInternal.Add(hubEvent);
    }
}