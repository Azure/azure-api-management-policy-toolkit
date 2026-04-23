// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

[AttributeUsage(AttributeTargets.Property)]
public class FragmentVariableAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
    public string DefaultValue { get; init; } = "";
}
