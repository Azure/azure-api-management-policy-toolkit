// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockLastError : ILastError
{
    public string Source { get; set; }
    public string Reason { get; set; }
    public string Message { get; set; }
    public string Scope { get; set; }
    public string Section { get; set; }
    public string Path { get; set; }
    public string PolicyId { get; set; }
}