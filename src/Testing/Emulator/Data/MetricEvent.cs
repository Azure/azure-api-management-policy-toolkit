// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

/// <summary>
/// An in-memory emit-metric policy invocation, available through TestDocument.SetupLoggerStore().Metrics.
/// No metrics are sent to external services and no aggregation or time-series quotas are simulated.
/// </summary>
/// <param name="Namespace">The configured namespace, or apim when omitted as documented by EmitMetricConfig.</param>
/// <param name="Name">The configured metric name.</param>
/// <param name="Value">The evaluated metric value, or 1 when omitted.</param>
/// <param name="Dimensions">
/// An immutable snapshot of explicit values and default dimensions resolved from the expression context.
/// Backend ID requires an explicit value because the emulator does not expose a backend identifier.
/// </param>
public record MetricEvent(
    string Namespace,
    string Name,
    double Value,
    ImmutableDictionary<string, string?> Dimensions);