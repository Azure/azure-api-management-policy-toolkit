// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IAzureVnetInfo
{
    int VnetTrafficTag { get; }
    int SubnetId { get; }
    int PrivateLinkId { get; }
    string? SnatVip { get; }
}
