// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class Logger(string loggerId)
{
    public string LoggerId => loggerId;
    internal readonly IList<EventHubEvent> EventsInternal = new SynchronizedList<EventHubEvent>();
    public ImmutableArray<EventHubEvent> Events => EventsInternal.ToImmutableArray();
}