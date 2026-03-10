// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Marks an expression method as a named value reference.
/// <para>
/// Simple named value: <c>[NamedValue("MyKey")]</c> emits <c>{{MyKey}}</c> in XML.
/// </para>
/// <para>
/// Template with embedded tokens: <c>[NamedValue("{{Api-Backend-Url}}/v2.0")]</c>
/// emits the string as-is. Auto-detected when the value contains <c>{{</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class NamedValueAttribute(string value) : Attribute
{
    public string Value { get; } = value;

    /// <summary>
    /// True when the value is a template containing embedded {{token}} references.
    /// </summary>
    public bool IsTemplate => Value.Contains("{{");
}
