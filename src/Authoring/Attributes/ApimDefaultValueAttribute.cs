// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Declares the APIM default value for an optional policy attribute.
/// When a property's value matches this default, the compiler will not emit the XML attribute
/// (APIM applies the default implicitly). The comparer also uses this to tolerate
/// differences where one side explicitly states the default and the other omits it.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ApimDefaultValueAttribute(string value, string xmlAttributeName) : Attribute
{
    public string Value { get; } = value;
    public string XmlAttributeName { get; } = xmlAttributeName;
}
