// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

/// <summary>
/// The XML body of a liquid set-body has to be written as markup, exactly as authored.<br/>
/// Verified against the gateway: a liquid template written as escaped text is returned still escaped, so it
/// has to be markup, while a body without a template that is written as markup is returned empty, so it has
/// to stay text.
/// </summary>
public static class RawXmlContent
{
    /// <summary>
    /// True for a liquid set-body that holds markup, which the writer must not indent.
    /// </summary>
    public static bool IsMarkupBody(XElement element) =>
        element.Name.LocalName == "set-body" && element.Attribute("template")?.Value == "liquid" &&
        element.HasElements;

    /// <summary>
    /// Adds the value to a liquid set-body as markup when it is well-formed XML with at least one element,
    /// also when it is apart from a &lt; or &amp; written as text; otherwise as plain text. Markup is written
    /// back in its normal form, such as double-quoted attributes.
    /// </summary>
    public static void AddTo(XElement element, string value)
    {
        if (element.Attribute("template")?.Value == "liquid" && TryParse(value, out var nodes))
        {
            element.Add(nodes);
        }
        else
        {
            element.Add(value);
        }
    }

    private static bool TryParse(string value, out List<XNode> nodes)
    {
        nodes = [];
        var trimmed = value.TrimStart();
        if (!value.Contains('<') || trimmed.StartsWith("@(") || trimmed.StartsWith("@{"))
        {
            return false;
        }

        // A < or & that doesn't start a tag or an entity, as in {% if item.Count < 2 %}, is text; the gateway
        // reads it back from its escaped form inside a liquid tag.
        return TryParseXml(value, out nodes) ||
               TryParseXml(StrayAmpersand.Replace(StrayLessThan.Replace(value, "&lt;"), "&amp;"), out nodes);
    }

    private static readonly Regex StrayLessThan = new(@"<(?![A-Za-z_/!?])", RegexOptions.Compiled);

    private static readonly Regex StrayAmpersand =
        new(@"&(?!(?:[A-Za-z][A-Za-z0-9]*|#[0-9]+|#x[0-9A-Fa-f]+);)", RegexOptions.Compiled);

    private static bool TryParseXml(string value, out List<XNode> nodes)
    {
        nodes = [];
        try
        {
            var wrapper = XElement.Parse($"<wrapper>{value}</wrapper>", LoadOptions.PreserveWhitespace);
            nodes = wrapper.Nodes().ToList();
            return nodes.OfType<XElement>().Any();
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
