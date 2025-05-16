// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockSubscriptionKeyParameterNames : ISubscriptionKeyParameterNames
{
    public string Header { get; set; } = "X-Sub-Header";
    public string Query { get; set; } = "subQuery";
}