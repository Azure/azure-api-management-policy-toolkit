// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockResponse : MockMessage, IResponse
{
    IMessageBody IResponse.Body => Body;

    IReadOnlyDictionary<string, string[]> IResponse.Headers => Headers;

    public int StatusCode { get; set; } = 200;

    public string StatusReason { get; set; } = "OK";
}