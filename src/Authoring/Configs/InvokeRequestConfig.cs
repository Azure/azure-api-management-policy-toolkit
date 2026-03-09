// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the custom invoke-request policy.<br/>
/// Invokes a request and either stores the result in a variable or short-circuits the current section with the returned response.
/// </summary>
public record InvokeRequestConfig
{
    /// <summary>
    /// Optional. The HTTP method to use for the outgoing request. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Method { get; init; }

    /// <summary>
    /// Optional. The target URL for the outgoing request. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? Url { get; init; }

    /// <summary>
    /// Optional. The backend identifier to invoke. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string? BackendId { get; init; }

    /// <summary>
    /// Optional. The variable name that receives the response. When omitted, the response is written to the current context response and section execution stops.
    /// </summary>
    public string? ResponseVariableName { get; init; }

    /// <summary>
    /// Optional. Request headers to include in the outgoing request.
    /// </summary>
    public HeaderConfig[]? Headers { get; init; }

    /// <summary>
    /// Optional. Request body content to include in the outgoing request.
    /// </summary>
    public BodyConfig? Body { get; init; }
}
