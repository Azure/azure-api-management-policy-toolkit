// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IApi
{
    string Id { get; }
    string Name { get; }
    string Path { get; }
    IEnumerable<string> Protocols { get; }
    IUrl ServiceUrl { get; }
    ISubscriptionKeyParameterNames SubscriptionKeyParameterNames { get; }
}