// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

public record DecompileOptions
{
    public string? Scope { get; init; }
    /// <summary>
    /// When set, emitted as the first positional argument in the [Document] attribute.
    /// For policies this is typically the relative path (e.g. "apis/myapi/operations/op1/policy.xml").
    /// For fragments the fragment ID is used instead.
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// The format the policy text is written in: rawxml unless stated, as for the policy compiler.
    /// </summary>
    public PolicyFormat PolicyFormat { get; init; } = PolicyFormat.RawXml;
}

/// <summary>
/// The format of the policy text given to the decompiler, as in the policy compiler's --policy-format.
/// </summary>
public enum PolicyFormat
{
    /// <summary>
    /// API Management's rawxml: a value that begins with a policy expression holds C# as it is written, so an
    /// entity in it is not decoded. Everything else is XML.
    /// </summary>
    RawXml,

    /// <summary>
    /// XML: a policy expression is XML-escaped like any other text, and the document has to be well-formed.
    /// </summary>
    Xml
}
