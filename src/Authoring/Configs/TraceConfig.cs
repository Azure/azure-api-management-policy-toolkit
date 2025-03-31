// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the trace policy.<br />
/// Specifies the trace source, message, severity, and optional metadata.
/// </summary>
public record TraceConfig
{
    /// <summary>
    /// The source of the trace message.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The trace message to be logged. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Message { get; init; }

    /// <summary>
    /// The severity of the trace message. Optional.
    /// </summary>
    public string? Severity { get; init; }

    /// <summary>
    /// Optional metadata to include with the trace message.
    /// </summary>
    public TraceMetadata[]? Metadata { get; init; }
}

/// <summary>
/// Metadata for the trace policy.
/// </summary>
public record TraceMetadata
{
    /// <summary>
    /// The name of the metadata item.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The value of the metadata item.
    /// </summary>
    public required string Value { get; init; }
}