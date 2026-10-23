// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

[AttributeUsage(AttributeTargets.Property)]
public class FragmentVariableAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    /// <summary>
    /// Gets the fallback for a null property value. Empty strings and other non-null values are preserved.
    /// </summary>
    public string DefaultValue { get; init; } = "";
}
