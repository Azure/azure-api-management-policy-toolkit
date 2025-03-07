// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record LimitConcurrencyConfig
{
    public required string Key { get; init; }
    public required int MaxCount { get; init; }
}