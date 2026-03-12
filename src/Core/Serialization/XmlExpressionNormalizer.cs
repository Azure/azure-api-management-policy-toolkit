// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

/// <summary>
/// Compares two XML policy documents element-by-element.
/// C# expressions (@{...}, @(...)) are extracted, stripped of all trivia
/// (including comments), formatted with Roslyn, and compared.
/// </summary>
public static class PolicyXmlComparer
{
    // Elements whose children can appear in any order (APIM treats them as sets)
    private static readonly HashSet<string> OrderIndependentChildren = new()
    {
        "vary-by-header", "vary-by-query-parameter",
        "allowed-origins", "allowed-headers", "allowed-methods", "expose-headers"
    };

    /// <summary>
    /// Auto-discovered registry of APIM XML attribute defaults.
    /// Built by scanning all config classes for <see cref="ApimDefaultValueAttribute"/>.
    /// Key = XML attribute name, Value = APIM default value.
    /// </summary>
    public static readonly Lazy<Dictionary<string, string>> ApimDefaults = new(BuildApimDefaults);

    private static Dictionary<string, string> BuildApimDefaults()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(ApimDefaultValueAttribute).Assembly.GetTypes())
        {
            foreach (var prop in type.GetProperties())
            {
                var attr = prop.GetCustomAttribute<ApimDefaultValueAttribute>();
                if (attr != null)
                    result.TryAdd(attr.XmlAttributeName, attr.Value);
            }
        }
        return result;
    }

    public static string? Compare(XElement expected, XElement actual)
    {
        if (expected.Name.LocalName != actual.Name.LocalName)
            return $"Element mismatch: expected <{expected.Name.LocalName}>, got <{actual.Name.LocalName}>";

        var expAttrs = expected.Attributes().OrderBy(a => a.Name.LocalName).ToList();
        var actAttrs = actual.Attributes().OrderBy(a => a.Name.LocalName).ToList();

        // Filter out attributes whose value matches the APIM default (they're semantically absent)
        expAttrs = FilterNonDefaultAttributes(expAttrs);
        actAttrs = FilterNonDefaultAttributes(actAttrs);

        if (expAttrs.Count != actAttrs.Count)
            return $"<{expected.Name.LocalName}> attribute count: expected {expAttrs.Count} " +
                   $"[{string.Join(", ", expAttrs.Select(a => a.Name.LocalName))}], " +
                   $"got {actAttrs.Count} [{string.Join(", ", actAttrs.Select(a => a.Name.LocalName))}]";

        for (int i = 0; i < expAttrs.Count; i++)
        {
            if (expAttrs[i].Name.LocalName != actAttrs[i].Name.LocalName)
                return $"<{expected.Name.LocalName}> attribute name: expected '{expAttrs[i].Name.LocalName}', got '{actAttrs[i].Name.LocalName}'";

            var diff = CompareAttributeValues(
                expAttrs[i].Name.LocalName, expAttrs[i].Value.Trim(), actAttrs[i].Value.Trim());
            if (diff != null)
                return $"<{expected.Name.LocalName}> @{expAttrs[i].Name.LocalName}: {diff}";
        }

        var expChildren = expected.Elements().ToList();
        var actChildren = actual.Elements().ToList();

        if (expChildren.Count != actChildren.Count)
            return $"<{expected.Name.LocalName}> child count: expected {expChildren.Count} " +
                   $"[{string.Join(", ", expChildren.Select(e => e.Name.LocalName))}], " +
                   $"got {actChildren.Count} [{string.Join(", ", actChildren.Select(e => e.Name.LocalName))}]";

        // Sort order-independent child groups before comparing
        SortOrderIndependentChildren(expChildren);
        SortOrderIndependentChildren(actChildren);

        for (int i = 0; i < expChildren.Count; i++)
        {
            var childDiff = Compare(expChildren[i], actChildren[i]);
            if (childDiff != null)
                return childDiff;
        }

        var expText = GetDirectText(expected);
        var actText = GetDirectText(actual);
        var textDiff = CompareValues(expText, actText);
        if (textDiff != null)
            return $"<{expected.Name.LocalName}> text: {textDiff}";

        return null;
    }

    /// <summary>
    /// Sorts contiguous runs of order-independent children (e.g., vary-by-header
    /// and vary-by-query-parameter) so their relative order doesn't matter.
    /// </summary>
    private static void SortOrderIndependentChildren(List<XElement> children)
    {
        int i = 0;
        while (i < children.Count)
        {
            if (OrderIndependentChildren.Contains(children[i].Name.LocalName))
            {
                int start = i;
                while (i < children.Count && OrderIndependentChildren.Contains(children[i].Name.LocalName))
                    i++;
                if (i - start > 1)
                {
                    var segment = children.GetRange(start, i - start);
                    segment.Sort((a, b) =>
                    {
                        int cmp = string.Compare(a.Name.LocalName, b.Name.LocalName, StringComparison.Ordinal);
                        return cmp != 0 ? cmp : string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
                    });
                    for (int j = 0; j < segment.Count; j++)
                        children[start + j] = segment[j];
                }
            }
            else
            {
                i++;
            }
        }
    }

    /// <summary>
    /// Removes attributes whose value matches the APIM default declared via
    /// <see cref="ApimDefaultValueAttribute"/>. These attributes are semantically
    /// equivalent to being absent, so they should not cause comparison differences.
    /// </summary>
    private static List<XAttribute> FilterNonDefaultAttributes(List<XAttribute> attrs)
    {
        var defaults = ApimDefaults.Value;
        return attrs
            .Where(a => !(defaults.TryGetValue(a.Name.LocalName, out var def) &&
                          string.Equals(a.Value.Trim(), def, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Compares attribute values, handling ISO 8601 duration equivalence
    /// (e.g., P30D == 2592000) for renewal-period attributes.
    /// </summary>
    private static string? CompareAttributeValues(string attrName, string expected, string actual)
    {
        if (expected == actual)
            return null;

        // ISO 8601 duration equivalence: P30D == 2592000 seconds
        if (attrName == "renewal-period" && TryParseIsoDuration(expected, out var expSeconds))
        {
            if (long.TryParse(actual, out var actSeconds) && expSeconds == actSeconds)
                return null;
        }
        if (attrName == "renewal-period" && TryParseIsoDuration(actual, out var actSec2))
        {
            if (long.TryParse(expected, out var expSec2) && actSec2 == expSec2)
                return null;
        }

        return CompareValues(expected, actual);
    }

    private static bool TryParseIsoDuration(string value, out long seconds)
    {
        seconds = 0;
        if (string.IsNullOrEmpty(value) || value[0] != 'P') return false;
        try
        {
            var ts = System.Xml.XmlConvert.ToTimeSpan(value);
            seconds = (long)ts.TotalSeconds;
            return true;
        }
        catch { return false; }
    }

    private static string GetDirectText(XElement element)
    {
        var sb = new StringBuilder();
        foreach (var node in element.Nodes().OfType<XText>())
            sb.Append(node.Value);
        return sb.ToString().Trim();
    }

    private static string? CompareValues(string expected, string actual)
    {
        if (expected == actual)
            return null;

        var expParts = ExtractParts(expected);
        var actParts = ExtractParts(actual);

        if (expParts.Count != actParts.Count)
            return $"part count mismatch: expected {expParts.Count}, got {actParts.Count}";

        for (int i = 0; i < expParts.Count; i++)
        {
            var ep = expParts[i];
            var ap = actParts[i];

            if (ep.IsExpression != ap.IsExpression)
                return $"part {i} type mismatch";

            if (ep.IsExpression)
            {
                var fmtExp = FormatCSharp(ep.Value.Trim());
                var fmtAct = FormatCSharp(ap.Value.Trim());
                if (fmtExp != fmtAct)
                {
                    int minLen = Math.Min(fmtExp.Length, fmtAct.Length);
                    int diffPos = minLen;
                    for (int j = 0; j < minLen; j++)
                        if (fmtExp[j] != fmtAct[j]) { diffPos = j; break; }
                    int start = Math.Max(0, diffPos - 40);
                    var expSnip = fmtExp.Substring(start, Math.Min(80, fmtExp.Length - start));
                    var actSnip = fmtAct.Substring(start, Math.Min(80, fmtAct.Length - start));
                    return $"code diff at pos {diffPos}: expected '...{expSnip}...', got '...{actSnip}...'";
                }
            }
            else
            {
                if (ep.Value.Trim() != ap.Value.Trim())
                    return $"expected '{Truncate(ep.Value.Trim(), 120)}', got '{Truncate(ap.Value.Trim(), 120)}'";
            }
        }

        return null;
    }

    private record struct ValuePart(string Value, bool IsExpression);

    private static List<ValuePart> ExtractParts(string value)
    {
        var parts = new List<ValuePart>();
        int i = 0, textStart = 0;

        while (i < value.Length)
        {
            if (i + 1 < value.Length && value[i] == '@' &&
                (value[i + 1] == '(' || value[i + 1] == '{'))
            {
                if (i > textStart)
                    parts.Add(new ValuePart(value[textStart..i], false));

                char open = value[i + 1];
                char close = open == '(' ? ')' : '}';
                i += 2;
                int end = FindExpressionEnd(value, i, open, close);
                parts.Add(new ValuePart(value[i..end], true));
                i = end + 1;
                textStart = i;
                continue;
            }
            i++;
        }

        if (textStart < value.Length)
            parts.Add(new ValuePart(value[textStart..], false));

        return parts;
    }

    /// <summary>
    /// Strip all trivia (whitespace + comments), format with Roslyn.
    /// Both sides get identical treatment — no special handling.
    /// </summary>
    private static string FormatCSharp(string code)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code,
                new CSharpParseOptions(kind: SourceCodeKind.Script));
            var root = tree.GetRoot();
            var stripped = new AllTriviaRemover().Visit(root);
            return stripped.NormalizeWhitespace().ToFullString().Trim();
        }
        catch
        {
            return code.Trim();
        }
    }

    /// <summary>Removes ALL trivia — whitespace, comments, everything.</summary>
    private sealed class AllTriviaRemover : CSharpSyntaxRewriter
    {
        public override SyntaxTriviaList VisitList(SyntaxTriviaList list) =>
            default;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    private static int FindExpressionEnd(string value, int start, char open, char close)
    {
        int depth = 1, i = start;
        bool inString = false, inVerbatim = false, inChar = false;

        while (i < value.Length && depth > 0)
        {
            char c = value[i];

            if (inChar)
            {
                if (c == '\\' && i + 1 < value.Length) { i += 2; continue; }
                if (c == '\'') inChar = false;
                i++; continue;
            }
            if (inString)
            {
                if (inVerbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < value.Length && value[i + 1] == '"') { i += 2; continue; }
                        inString = false; inVerbatim = false;
                    }
                }
                else
                {
                    if (c == '\\' && i + 1 < value.Length) { i += 2; continue; }
                    if (c == '"') inString = false;
                }
                i++; continue;
            }

            if (c == '\'') { inChar = true; i++; continue; }
            if (c == '@' && i + 1 < value.Length && value[i + 1] == '"')
            { inString = true; inVerbatim = true; i += 2; continue; }
            if (c == '$' && i + 1 < value.Length && value[i + 1] == '"')
            { inString = true; i += 2; continue; }
            if (c == '$' && i + 2 < value.Length && value[i + 1] == '@' && value[i + 2] == '"')
            { inString = true; inVerbatim = true; i += 3; continue; }
            if (c == '"') { inString = true; i++; continue; }

            // Skip over comments so brackets inside don't affect depth
            if (c == '/' && i + 1 < value.Length && value[i + 1] == '/')
            { i += 2; while (i < value.Length && value[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < value.Length && value[i + 1] == '*')
            { i += 2; while (i + 1 < value.Length && !(value[i] == '*' && value[i + 1] == '/')) i++; if (i + 1 < value.Length) i += 2; continue; }

            if (c == open) depth++;
            else if (c == close) { depth--; if (depth == 0) return i; }
            i++;
        }
        return i;
    }
}
