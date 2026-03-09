// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockRequest : MockMessage, IRequest
{
    public MockRequest() { }

    public MockRequest(Uri uri)
    {
        var mockUrl = new MockUrl(uri);
        Url = mockUrl;
        OriginalUrl = new MockUrl(uri);
    }

    IMessageBody IRequest.Body => Body;

    public X509Certificate2? Certificate { get; set; } = null;

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

    public MockAzureVnetInfo? AzureVnetInfo { get; set; }
    IAzureVnetInfo? IRequest.AzureVnetInfo => AzureVnetInfo;

    public MockRequest WithMatchedParameters(IDictionary<string, string> parameters)
    {
        foreach (var kvp in parameters)
            MatchedParameters[kvp.Key] = kvp.Value;
        return this;
    }

    public MockRequest WithClientCertificate(X509Certificate2 certificate)
    {
        Certificate = certificate;
        return this;
    }
}