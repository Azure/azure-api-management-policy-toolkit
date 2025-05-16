// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IRequest
{
    IMessageBody? Body { get; }

    X509Certificate2? Certificate { get; }

    IReadOnlyDictionary<string, string[]> Headers { get; }

    string IpAddress { get; }

    IReadOnlyDictionary<string, string> MatchedParameters { get; }

    string Method { get; }

    IUrl OriginalUrl { get; }

    IUrl Url { get; }

    IPrivateEndpointConnection? PrivateEndpointConnection { get; }
}