// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockBackend : IBackend
{
    public string AzureRegion { get; set; } = "eastus";
    public string Id { get; set; } = "backend";
    public string Type { get; set; } = "Single";
}
