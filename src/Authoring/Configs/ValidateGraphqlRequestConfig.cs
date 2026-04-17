// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the validate-graphql-request policy.
/// </summary>
public record ValidateGraphqlRequestConfig
{
    [ExpressionAllowed]
    public string? ErrorVariableName { get; init; }

    [ExpressionAllowed]
    public int? MaxDepth { get; init; }

    [ExpressionAllowed]
    public long? MaxSize { get; init; }

    [ExpressionAllowed]
    public int? MaxTotalDepth { get; init; }

    [ExpressionAllowed]
    public int? MaxComplexity { get; init; }

    public AuthorizeConfig? Authorize { get; init; }
}

public record AuthorizeConfig
{
    public required AuthorizeRuleConfig[] Rules { get; init; }
}

public record AuthorizeRuleConfig
{
    public required string Path { get; init; }
    public string? Action { get; init; }
}
