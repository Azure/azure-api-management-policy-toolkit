// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockWorkspace : IWorkspace
{
    public string Id { get; set; } = "workspace";
    public string Name { get; set; } = "workspace";
}
