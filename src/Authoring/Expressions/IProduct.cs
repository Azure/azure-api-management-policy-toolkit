// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IProduct
{
    IEnumerable<IApi> Apis { get; }
    bool ApprovalRequired { get; }
    IEnumerable<IGroup> Groups { get; }
    string Id { get; }
    string Name { get; }
    ProductState State { get; }
    int? SubscriptionLimit { get; }
    bool SubscriptionRequired { get; }
}

public enum ProductState { NotPublished, Published }