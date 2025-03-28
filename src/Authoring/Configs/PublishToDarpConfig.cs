// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record PublishToDarpConfig
{
    [ExpressionAllowed] public required string Topic { get; init; }
    [ExpressionAllowed] public required string Content { get; init; }

    [ExpressionAllowed] public string? PubSubName { get; init; }
    public bool? IgnoreError { get; init; }
    public string? ResponseVariableName { get; init; }
    public int? Timeout { get; init; }
    public string? Template { get; init; }
    public string? ContentType { get; init; }
}