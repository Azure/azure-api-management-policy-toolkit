// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class Logger(string loggerId)
{
    public string LoggerId => loggerId;
    internal readonly SynchronizedList<EventHubEvent> EventsInternal = new();
    public ImmutableArray<EventHubEvent> Events => EventsInternal.Snapshot();
}