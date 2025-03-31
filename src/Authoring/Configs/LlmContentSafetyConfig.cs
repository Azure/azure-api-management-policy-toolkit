// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the LLM content safety policy.
/// </summary>
public record LlmContentSafetyConfig
{
    /// <summary>
    /// Specifies the backend service ID to be used for content safety evaluation. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string BackendId { get; init; }

    /// <summary>
    /// Specifies whether to shield the prompt from being sent to the backend service. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public bool? ShieldPrompt { get; init; }

    /// <summary>
    /// Specifies the content safety categories to be evaluated.
    /// </summary>
    public ContentSafetyCategories? Categories { get; init; }

    /// <summary>
    /// Specifies the block lists to be used for content safety evaluation.
    /// </summary>
    public ContentSafetyBlockLists? BlockLists { get; init; }
}

/// <summary>
/// Configuration for content safety categories.
/// </summary>
public record ContentSafetyCategories
{
    /// <summary>
    /// Specifies the output type for the content safety evaluation. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? OutputType { get; init; }

    /// <summary>
    /// Specifies the list of content safety categories.
    /// </summary>
    public ContentSafetyCategory[]? Categories { get; init; }
}

/// <summary>
/// Configuration for a content safety category.
/// </summary>
public record ContentSafetyCategory
{
    /// <summary>
    /// Specifies the name of the content safety category. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Name { get; init; }

    /// <summary>
    /// Specifies the threshold for the content safety category. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required int Threshold { get; init; }
}

/// <summary>
/// Configuration for content safety block lists.
/// </summary>
public record ContentSafetyBlockLists
{
    /// <summary>
    /// Specifies the IDs of the block lists to be used for content safety evaluation. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string[] Ids { get; init; }
}