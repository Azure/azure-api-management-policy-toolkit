// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockApi : IApi
{
    public string Id { get; set; } = "CVjSPi5XPE";
    public string Name { get; set; } = "mock-api";

    public string Path { get; set; } = "/mock";

    public IEnumerable<string> Protocols { get; set; } = ["https"];

    public MockUrl ServiceUrl { get; set; } = new MockUrl();
    IUrl IApi.ServiceUrl => ServiceUrl;

    public MockSubscriptionKeyParameterNames SubscriptionKeyParameterNames { get; set; } =
        new MockSubscriptionKeyParameterNames();

    ISubscriptionKeyParameterNames IApi.SubscriptionKeyParameterNames => SubscriptionKeyParameterNames;
}