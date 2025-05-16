// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface ISubscriptionKeyParameterNames
{
    string Header { get; }
    string Query { get; }
}