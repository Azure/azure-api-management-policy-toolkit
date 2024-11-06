// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IUserIdentity
{
    string Id { get; }
    string Provider { get; }
}