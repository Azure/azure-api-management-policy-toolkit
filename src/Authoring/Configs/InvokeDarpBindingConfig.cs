// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record InvokeDarpBindingConfig
{
    [ExpressionAllowed] public required string Name { get; init; }
    public string? Operation { get; init; }
    public bool? IgnoreError { get; init; }
    public string? ResponseVariableName { get; init; }
    [ExpressionAllowed] public int? Timeout { get; init; }
    public string? Template { get; init; }
    public string? ContentType { get; init; }

    public DarpMetaData[]? MetaData { get; init; }
    [ExpressionAllowed] public string? Data { get; init; }
}

public record DarpMetaData
{
    public required string Key { get; init; }
    [ExpressionAllowed] public required string Value { get; init; }
}