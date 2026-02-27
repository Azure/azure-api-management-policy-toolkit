// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class MetricStore
{
    internal readonly IList<EmittedMetric> MetricsInternal = new List<EmittedMetric>();

    public ImmutableArray<EmittedMetric> Metrics => MetricsInternal.ToImmutableArray();
}
