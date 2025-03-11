// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record QuotaByKeyConfig
{
    public required string CounterKey { get; init; }
    public required int RenewalPeriod { get; init; }
    public int? Calls { get; init; }
    public int? Bandwidth { get; init; }

    public bool? IncrementCondition { get; init; }
    public int? IncrementCount { get; init; }
    public string? FirstPeriodStart { get; init; }
}