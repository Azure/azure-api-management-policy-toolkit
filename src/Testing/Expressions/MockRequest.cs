// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

using Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockRequest : IRequest
{

    public MockBody Body { get; set; } = new MockBody();
    IMessageBody IRequest.Body => Body;


    public X509Certificate2? Certificate { get; set; } = null;

    public Dictionary<string, string[]> MockHeaders { get; set; } = new Dictionary<string, string[]>()
    {
        {"Accept", ["application/json"] }
    };

    public Dictionary<string, string[]> Headers { get; set; } = new Dictionary<string, string[]>();
    IReadOnlyDictionary<string, string[]> IRequest.Headers => Headers;

    public string IpAddress { get; set; } = "192.168.0.1";

    public Dictionary<string, string> MatchedParameters { get; set; } = new Dictionary<string, string>();
    IReadOnlyDictionary<string, string> IRequest.MatchedParameters => MatchedParameters;

    public string Method { get; set; } = "GET";

    public MockUrl OriginalUrl { get; set; } = new MockUrl();
    IUrl IRequest.OriginalUrl => OriginalUrl;

    public MockUrl Url { get; set; } = new MockUrl();
    IUrl IRequest.Url => Url;

    public MockPrivateEndpointConnection? PrivateEndpointConnection { get; set; }
    IPrivateEndpointConnection? IRequest.PrivateEndpointConnection => PrivateEndpointConnection;
}