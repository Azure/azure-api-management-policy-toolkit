// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the mock-response policy that returns a fabricated response directly to the caller.
/// </summary>
public record MockResponseConfig
{
    /// <summary>
    /// HTTP status code to be returned. Default is 200 OK.<br/>
    /// Policy expressions aren't allowed.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Value of Content-Type HTTP header to be returned. Default is application/json.<br/>
    /// Policy expressions aren't allowed.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Specifies which response, from an Examples entity defined in API Management, to return.<br/>
    /// When specified, the body of the example is used as the mocked response body.<br/>
    /// Policy expressions aren't allowed.
    /// </summary>
    public int? Index { get; init; }
}