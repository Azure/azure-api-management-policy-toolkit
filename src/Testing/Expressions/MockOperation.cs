// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockOperation : IOperation
{
    public string Id { get; set; } = "operation-id";
    public string Method { get; set; } = "GET";
    public string Name { get; set; } = "fetch-operation";
    public string UrlTemplate { get; set; } = "/v2/mock/op";
}