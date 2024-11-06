// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public record SendRequestConfig
{
    public required string ResponseVariableName { get; init; }
    
    public string? Mode { get; init; }
    public int? Timeout { get; init; }
    public bool? IgnoreError { get; init; }
    
    public string? Url { get; init; }
    public string? Method { get; init; }
    public HeaderConfig[]? Headers { get; init; }
    public BodyConfig? Body { get; init; }
    public IAuthenticationConfig? Authentication { get; init; }
    public ProxyConfig? Proxy { get; init; }
}
