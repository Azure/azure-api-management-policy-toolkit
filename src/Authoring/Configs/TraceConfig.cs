// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record TraceConfig
{
    public required string Source { get; init; }
    public required string Message { get; init; }
    public string? Severity { get; init; }
    public TraceMetadata[]? Metadata { get; init; }
}

public record TraceMetadata
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}