// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class SectionAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}