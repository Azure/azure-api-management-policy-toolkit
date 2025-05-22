// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockProduct : IProduct
{
    public IEnumerable<IApi> Apis { get; set; }
    public bool ApprovalRequired { get; set; }
    public IEnumerable<IGroup> Groups { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public ProductState State { get; set; }
    public int? SubscriptionLimit { get; set; }
    public bool SubscriptionRequired { get; set; }
}