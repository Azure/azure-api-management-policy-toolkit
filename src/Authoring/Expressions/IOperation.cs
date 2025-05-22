// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IOperation
{
    string Id { get; }
    string Method { get; }
    string Name { get; }
    string UrlTemplate { get; }
}