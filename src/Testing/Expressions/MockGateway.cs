// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockGateway : IGateway
{
    public string Id { get; set; } = "managed";
    public string InstanceId { get; set; } = "managed";
    public bool IsManaged { get; set; } = true;
}
