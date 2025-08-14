// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

[AttributeUsage(AttributeTargets.Class)]
public class DocumentAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
    public DocumentScope Scope { get; init; } = DocumentScope.Any;
    public DocumentType Type { get; init; } = DocumentType.Policy;
}