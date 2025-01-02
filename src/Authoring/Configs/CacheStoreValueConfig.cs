// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record CacheStoreValueConfig
{
    public required string Key { get; init; }
    public required object Value { get; init; }
    public required uint Duration { get; init; }
    public string? CachingType { get; init; }
}