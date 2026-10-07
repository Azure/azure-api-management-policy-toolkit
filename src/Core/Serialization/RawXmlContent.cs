// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

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
    /// True for a liquid set-body that holds markup (an element or a CDATA section), which the writer must not
    /// indent.
    /// </summary>
    public static bool IsMarkupBody(XElement element) =>
        element.Name.LocalName == "set-body" && element.Attribute("template")?.Value == "liquid" &&
        element.Nodes().Any(IsMarkup) && !IsValueElementBody(element);

    /// <summary>
    /// True for a body given through a value element, which is a policy value and not a template written in
    /// place: the value element is all there is in it.
    /// </summary>
    public static bool IsValueElementBody(XElement element) =>
        element.Nodes().Where(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value) || text is XCData)
            .ToList() is [XElement { Name.LocalName: "value" }];

    private static bool IsMarkup(XNode node) => node is XElement or XCData;

    /// <summary>
    /// Adds the value to a liquid set-body as markup when it is well-formed XML with at least one element or
    /// CDATA section,
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
        // a body that is one policy expression is code, not a template
        if (!value.Contains('<') || PolicyDecompilerContext.IsSingleExpression(value.Trim()))
        {
            return false;
        }

        // A < or & that doesn't start a tag or an entity, as in {% if item.Count < 2 %}, is text; the gateway
        // reads it back from its escaped form inside a liquid tag.
        return TryParseXml(value, out nodes) || TryParseXml(StrayCharacter.Replace(value, Escape), out nodes);
    }

    // A CDATA section or a comment, which is kept as it is, or a < or & that starts neither a tag nor an entity. A
    // tag starts with a letter of any script, _ or :. A policy has no DTD, so the only entities are XML's five and
    // character references; an HTML entity such as &nbsp; is text.
    private static readonly Regex StrayCharacter = new(
        @"<!\[CDATA\[.*?\]\]>|<!--.*?-->|<(?![\p{L}_:/!?])|&(?!(?:" + Entities + @");)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // What an entity may be in a policy, which has no DTD: XML's five and a character reference.
    internal const string Entities = "lt|gt|amp|quot|apos|#[0-9]+|#x[0-9A-Fa-f]+";

    private static string Escape(Match match) => match.Value switch
    {
        "<" => "&lt;",
        "&" => "&amp;",
        _ => match.Value
    };

    private static bool TryParseXml(string value, out List<XNode> nodes)
    {
        nodes = [];
        try
        {
            var wrapper = XElement.Parse($"<wrapper>{value}</wrapper>", LoadOptions.PreserveWhitespace);
            nodes = wrapper.Nodes().ToList();
            return nodes.Any(IsMarkup);
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
