// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockContextApi : MockApi, IContextApi
{
    public bool IsCurrentRevision { get; set; } = true;

    public string Revision { get; set; } = "2";

    public string Version { get; set; } = "v2";
}