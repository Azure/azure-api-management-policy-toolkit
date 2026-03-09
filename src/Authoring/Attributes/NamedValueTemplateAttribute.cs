// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Marks an expression method as a named value template - a literal string
/// with embedded {{named-value}} tokens. The compiler emits the Template
/// string directly as the attribute value instead of wrapping in @(...).
/// Example: Template = "{{Api-Backend-Url}}/v2.0/prediction"
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class NamedValueTemplateAttribute(string template) : Attribute
{
    public string Template { get; } = template;
}
