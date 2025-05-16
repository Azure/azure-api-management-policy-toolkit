// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockSubscription : ISubscription
{
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow.AddDays(-3);

    public DateTime? EndDate { get; set; }

    public string Id { get; set; } = "MBJsUa3qCI";

    public string Key { get; set; } = "jkgvggusT9";

    public string Name { get; set; } = "mock-api-access";

    public string PrimaryKey { get; set; } = "AGfCkbqC6J5wu1n1AnGrl8p5eC5pirHD";

    public string SecondaryKey { get; set; } = "Of0lkMYsLVnt9f7WfWHBZn2GhZlHfgAJ";

    public DateTime? StartDate { get; set; }
}