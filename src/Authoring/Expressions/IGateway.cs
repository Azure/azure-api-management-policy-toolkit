// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IGateway
{
    string Id { get; }

    string InstanceId { get; }

    bool IsManaged { get; }
}
