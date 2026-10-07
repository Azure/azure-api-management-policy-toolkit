// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Decompiling;

/// <summary>
/// The decompiler for tests whose policy text is in either format: text that is well-formed XML is read as
/// xml and anything else as rawxml, unless the test states the format it is about.
/// </summary>
internal sealed class TestDecompiler
{
    private readonly PolicyDecompiler _decompiler = new();

    public string DecompileDocument(
        string xml,
        string className,
        string namespaceName,
        DecompileOptions? options = null,
        PolicyFormat? format = null) =>
        _decompiler.DecompileDocument(xml, className, namespaceName, WithFormat(options, format, xml));

    public string DecompileFragment(
        string xml,
        string fragmentId,
        string className,
        string namespaceName,
        DecompileOptions? options = null,
        PolicyFormat? format = null) =>
        _decompiler.DecompileFragment(xml, fragmentId, className, namespaceName, WithFormat(options, format, xml));

    public static string Preprocess(string xml) => PolicyDecompiler.PreprocessXml(xml, FormatOf(xml));

    public static PolicyFormat FormatOf(string xml)
    {
        try
        {
            XDocument.Parse(xml);
            return PolicyFormat.Xml;
        }
        catch (XmlException)
        {
            return PolicyFormat.RawXml;
        }
    }

    private static DecompileOptions WithFormat(DecompileOptions? options, PolicyFormat? format, string xml) =>
        (options ?? new DecompileOptions()) with { PolicyFormat = format ?? FormatOf(xml) };
}
