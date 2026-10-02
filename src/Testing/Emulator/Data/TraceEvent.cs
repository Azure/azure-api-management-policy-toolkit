// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

/// <summary>
/// An in-memory trace policy invocation, available through TestDocument.SetupLoggerStore().Traces.
/// </summary>
/// <param name="Source">The configured trace source.</param>
/// <param name="Message">The evaluated trace message.</param>
/// <param name="Severity">The configured severity, or verbose when omitted.</param>
/// <param name="Metadata">An immutable snapshot of the evaluated metadata.</param>
public record TraceEvent(
    string Source,
    string Message,
    string Severity,
    ImmutableDictionary<string, string> Metadata);