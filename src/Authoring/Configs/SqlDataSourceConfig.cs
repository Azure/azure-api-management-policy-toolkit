// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the <a href="https://learn.microsoft.com/en-us/azure/api-management/sql-data-source-policy">sql-data-source</a> policy.<br/>
/// Resolves a request to a backend SQL database.
/// </summary>
public record SqlDataSourceConfig
{
    public required SqlConnectionInfoConfig ConnectionInfo { get; init; }
    public required SqlRequestConfig Request { get; init; }
    [ExpressionAllowed] public string? SingleResult { get; init; }
    [ExpressionAllowed] public string? Timeout { get; init; }
}

public record SqlConnectionInfoConfig
{
    [ExpressionAllowed] public required string ConnectionString { get; init; }
    [ExpressionAllowed] public string? UseManagedIdentity { get; init; }
    [ExpressionAllowed] public string? ClientId { get; init; }
}

public record SqlRequestConfig
{
    [ExpressionAllowed] public required string SqlStatement { get; init; }
    public SqlParameterConfig[]? Parameters { get; init; }
}

public record SqlParameterConfig
{
    public required string Name { get; init; }
    public required string SqlType { get; init; }
    [ExpressionAllowed] public required string Value { get; init; }
}