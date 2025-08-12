// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

[AttributeUsage(AttributeTargets.Class)]
public class DocumentAttribute(string? name = null, DocumentScope scope = DocumentScope.Any, DocumentType type = DocumentType.Policy) : Attribute
{
    public string? Name { get; } = name;
    public DocumentScope Scope { get; } = scope;
    public DocumentType Type { get; } = type;
}