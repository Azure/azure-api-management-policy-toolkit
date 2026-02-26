// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record EmittedMetric(
    string Name,
    double Value,
    string? Namespace,
    MetricDimensionConfig[] Dimensions);
