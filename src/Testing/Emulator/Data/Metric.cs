// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public record MetricDimension(string Name, string Value);

public record Metric(string Namespace, string Name, double Value, MetricDimension[] Dimensions);