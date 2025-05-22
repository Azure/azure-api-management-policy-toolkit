// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Data;

public class DiagnosticStore
{
    public bool Enabled { get; set; } = false;
    public bool SkipDimensionOnEmpty { get; set; } = false;

    public List<Metric> Metrics { get; set; } = new();
}