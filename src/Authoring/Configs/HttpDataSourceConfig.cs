// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the http-data-source policy.
/// </summary>
public record HttpDataSourceConfig
{
    [ExpressionAllowed]
    public string? Url { get; init; }
    public string? Method { get; init; }
    public HeaderConfig[]? Headers { get; init; }
    public BodyConfig? Body { get; init; }
    public IAuthenticationConfig? Authentication { get; init; }
    public HeaderConfig[]? ResponseHeaders { get; init; }
    public BodyConfig? ResponseBody { get; init; }
}
