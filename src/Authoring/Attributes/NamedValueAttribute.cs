// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Marks an expression method as a named value reference.
/// The compiler emits {{Name}} in XML instead of an @(...) expression.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class NamedValueAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
