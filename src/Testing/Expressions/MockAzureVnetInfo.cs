// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockAzureVnetInfo : IAzureVnetInfo
{
    public int VnetTrafficTag { get; set; }
    public int SubnetId { get; set; }
    public int PrivateLinkId { get; set; }
    public string? SnatVip { get; set; }
}
