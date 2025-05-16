// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the return-response policy, specifying details of the response to return directly to the caller.
/// </summary>
public record ReturnResponseConfig
{
    /// <summary>
    /// Name of the variable containing the response object to return. If specified, other properties are ignored.
    /// </summary>
    public string? ResponseVariableName { get; init; }

    /// <summary>
    /// Configuration specifying the HTTP status code and reason phrase to return.
    /// </summary>
    public StatusConfig? Status { get; init; }

    /// <summary>
    /// Collection of headers to include in the response.
    /// </summary>
    public HeaderConfig[]? Headers { get; init; }

    /// <summary>
    /// Configuration specifying the body content of the response.
    /// </summary>
    public BodyConfig? Body { get; init; }
}